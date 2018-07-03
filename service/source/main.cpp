/*
 *
 *   Pwease dont steal my code >:3
 *
 */

#include <string.h>
#include <stdio.h>
#include <stdarg.h>
#include <unistd.h>
#include <stdlib.h>
#include <time.h>
#include <switch.h>
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/types.h>
#include <sys/socket.h>
#include <errno.h>

#ifdef __SYSMODULE__
    #define ERPT_SAVE_ID 0x80000000000000D1
    #define TITLE_ID 0x4200006900000000
    #define HEAP_SIZE 0x10000

    // we aren't an applet
    u32 __nx_applet_type = AppletType_None;
    // setup a fake heap (we don't need the heap anyway)
    char   fake_heap[HEAP_SIZE];

    void fatalLater(Result err) {
    #ifdef DEBUG
        Handle srv;

        while (R_FAILED(smGetServiceOriginal(&srv, smEncodeName("fatal:u")))) {
            // wait one sec and retry
            svcSleepThread(1000000000L);
        }

        // fatal is here time, fatal like a boss
        IpcCommand c;
        ipcInitialize(&c);
        ipcSendPid(&c);
        struct {
            u64 magic;
            u64 cmd_id;
            u64 result;
            u64 unknown;
        } *raw;

        raw = ipcPrepareHeader(&c, sizeof(*raw));

        raw->magic = SFCI_MAGIC;
        raw->cmd_id = 1;
        raw->result = err;
        raw->unknown = 0;

        ipcDispatch(srv);
        svcCloseHandle(srv);
    #else
        (void)err;
        svcExitProcess();
        __builtin_unreachable();
    #endif
    }

    void __libnx_initheap(void) {
        extern char* fake_heap_start;
        extern char* fake_heap_end;

        // setup newlib fake heap
        fake_heap_start = fake_heap;
        fake_heap_end   = fake_heap + HEAP_SIZE;
    }

    // override default initalization so we don't appletInitialize
    void __appInit(void) {
        Result rc;

        rc = smInitialize();
        if (R_FAILED(rc))
            fatalLater(rc);

        rc = fsInitialize();
        if (R_FAILED(rc))
            fatalLater(rc);

        rc = pmdmntInitialize();
        if (R_FAILED(rc))
            fatalLater(rc);
    }

    void __appExit(void) {
        pmdmntExit();
        fsdevUnmountAll();
        fsExit();
        smExit();
    }

    bool HOMEBREW_LAUNCHER = false;
#else
    bool HOMEBREW_LAUNCHER = true;
#endif

void clear() {
    if (!HOMEBREW_LAUNCHER)
        return;

    for (int i = 1; i <= 45; i++)
        printf("\x1b[%d;1H%s", i, "                                                                                ");
}
void print(char x, char y, char align, const char* string, ...) {
    if (!HOMEBREW_LAUNCHER)
        return;

    va_list args;
    va_start(args, string);

    char str[80];
    vsprintf(str, string, args);

    char xoff = (char)strlen(str);
    if (align == 0)
        xoff = 0;
    if (align == 1)
        xoff /= 2;

    printf("\x1b[%d;%dH%s", y + 1, x - xoff + 1, str);
}
void fprint(const char* string, ...) {
    va_list args;
    va_start(args, string);

    char str[256];
    vsprintf(str, string, args);

    FILE* f = fopen("log_komodo.txt", "a");
    fprintf(f, "%s\n", str);
    fclose(f);
}

enum class ConnectionType {
    USB,
    TCP
};

ConnectionType connectionType = ConnectionType::USB;

int sock;

static u32 usb_interface = 0;

size_t transport_safe_read(void* buffer, size_t size) {
	u8* bufptr = (u8*)buffer;
	size_t cursize = size;
	size_t tmpsize = 0;
	while (cursize) {
		tmpsize = usbCommsReadEx(bufptr, cursize, usb_interface);
		bufptr += tmpsize;
		cursize -= tmpsize;
	}
	return size;
}
size_t transport_safe_write(const void* buffer, size_t size) {
	u8* bufptr = (u8*)buffer;
	size_t cursize = size;
	size_t tmpsize = 0;
	while (cursize) {
		tmpsize = usbCommsWriteEx(bufptr, cursize, usb_interface);
		bufptr += tmpsize;
		cursize -= tmpsize;
	}
	return size;
}
size_t usbRead(void* buffer, size_t size) {
    return transport_safe_read(buffer, size);
}
size_t usbWrite(const void* buffer, size_t size) {
    return transport_safe_write(buffer, size);
}

size_t tcpRead(void* buffer, size_t size) {
    unsigned char* buf = (unsigned char*)buffer;
    size_t total = 0;
    while (total < size) {
        size_t count = recv(sock, buf, size - total, 0);
        if (count <= 0)
            exit(0);
        total += count;
        buf += count;
    }
    return size;
}
size_t tcpWrite(const void* buffer, size_t size) {
    unsigned char* buf = (unsigned char*)buffer;
    size_t total = 0;
    while (total < size) {
        size_t count = send(sock, buf, size - total, 0);
        if (count <= 0)
            exit(0);
        total += count;
        buf += count;
    }
    return size;
}

size_t connRead(void* buffer, size_t size) {
    if (connectionType == ConnectionType::USB)
        return usbRead(buffer, size);
    else if (connectionType == ConnectionType::TCP)
        return tcpRead(buffer, size);
    return 0;
}
size_t connWrite(const void* buffer, size_t size) {
    if (connectionType == ConnectionType::USB)
        return usbWrite(buffer, size);
    else if (connectionType == ConnectionType::TCP)
        return tcpWrite(buffer, size);
    return 0;
}

typedef enum {
    REQ_LIST_PROCESSES    = 0,
    REQ_ATTACH_PROCESS    = 1,
    REQ_DETACH_PROCESS    = 2,
    REQ_QUERYMEMORY       = 3,
    REQ_GET_DBGEVENT      = 4,
    REQ_READMEMORY        = 5,
    REQ_CONTINUE_DBGEVENT = 6,
    REQ_GET_THREADCONTEXT = 7,
    REQ_BREAK_PROCESS     = 8,
    REQ_WRITEMEMORY       = 9,
    REQ_LISTENAPPLAUNCH   = 10,
    REQ_GETAPPPID         = 11,
    REQ_START_PROCESS     = 12,
    REQ_GET_TITLE_PID     = 13
} RequestType;

typedef struct {
    u32 Type;
} DebuggerRequest;

typedef struct {
    u32 Result;
    u32 LenBytes;
    void* Data;
} DebuggerResponse;

typedef struct { // Cmd1
    u64 Pid;
} AttachProcessReq;

typedef struct {
    u32 DbgHandle;
} AttachProcessResp;

typedef struct { // Cmd2
    u32 DbgHandle;
} DetachProcessReq;

typedef struct { // Cmd3
    u32 DbgHandle;
    u32 Pad;
    u64 Addr;
} QueryMemoryReq;

typedef struct {
    u64 Addr;
    u64 Size;
    u32 Perm;
    u32 Type;
} QueryMemoryResp;

typedef struct { // Cmd4
    u32 DbgHandle;
} GetDbgEventReq;

typedef struct {
    u8 Event[0x40];
} GetDbgEventResp;

typedef struct { // Cmd5
    u32 DbgHandle;
    u32 Size;
    u64 Addr;
} ReadMemoryReq;

typedef struct { // Cmd6
    u32 DbgHandle;
    u32 Flags;
    u64 ThreadId;
} ContinueDbgEventReq;

typedef struct { // Cmd7
    u32 DbgHandle;
    u32 Flags;
    u64 ThreadId;
} GetThreadContextReq;

typedef struct {
    u8 Out[0x320];
} GetThreadContextResp;

typedef struct { // Cmd8
    u32 DbgHandle;
} BreakProcessReq;

typedef struct { // Cmd9
    u32 DbgHandle;
    u32 Value;
    u64 Addr;
} WriteMemoryReq;

typedef struct { // Cmd11
    u64 Pid;
} GetAppPidResp;

typedef struct { // Cmd12
    u64 Pid;
} StartProcessReq;

typedef struct { // Cmd13
    u64 TitleId;
} GetTitlePidReq;

typedef struct {
    u64 Pid;
} GetTitlePidResp;

void sendResponse(DebuggerResponse resp) {
    connWrite((void*)&resp, 8);

    if (resp.LenBytes > 0)
        connWrite(resp.Data, resp.LenBytes);
}

int handleCommand() {
    DebuggerRequest r;
    DebuggerResponse resp;
    Result rc;

    size_t len = connRead(&r, sizeof(r));
    // Transfer failure.
    if (len != sizeof(r))
        fatalSimple(222 | (1 << 9));

    resp.LenBytes = 0;
    resp.Data = NULL;

    switch (r.Type) {
        case REQ_LIST_PROCESSES: { // Cmd0
            static u64 pids[256];
            u32 numOut = 256;

            rc = svcGetProcessList(&numOut, pids, numOut);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = numOut * sizeof(u64);
                resp.Data = &pids[0];
            }

            sendResponse(resp);
            break;
        }
        case REQ_ATTACH_PROCESS: { // Cmd1
            AttachProcessReq   req_;
            AttachProcessResp  resp_;
            connRead(&req_, sizeof(req_));

            rc = svcDebugActiveProcess(&resp_.DbgHandle, req_.Pid);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = sizeof(resp_);
                resp.Data = &resp_;
            }

            sendResponse(resp);
            break;
        }
        case REQ_DETACH_PROCESS: { // Cmd2
            DetachProcessReq req_;
            connRead(&req_, sizeof(req_));

            rc = svcCloseHandle(req_.DbgHandle);
            resp.Result = rc;

            sendResponse(resp);
            break;
        }
        case REQ_QUERYMEMORY: { // Cmd3
            QueryMemoryReq   req_;
            QueryMemoryResp  resp_;
            connRead(&req_, sizeof(req_));

            MemoryInfo info;
            u32 who_cares;
            rc = svcQueryDebugProcessMemory(&info, &who_cares, req_.DbgHandle, req_.Addr);
            resp.Result = rc;

            if (rc == 0) {
                resp_.Addr = info.addr;
                resp_.Size = info.size;
                resp_.Type = info.type;
                resp_.Perm = info.perm;

                resp.LenBytes = sizeof(resp_);
                resp.Data = &resp_;
            }

            sendResponse(resp);
            break;
        }
        case REQ_GET_DBGEVENT: { // Cmd4
            GetDbgEventReq   req_;
            GetDbgEventResp  resp_;
            connRead(&req_, sizeof(req_));

            rc = svcGetDebugEvent(&resp_.Event[0], req_.DbgHandle);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = sizeof(resp_);
                resp.Data = &resp_;
            }

            sendResponse(resp);
            break;
        }
        case REQ_READMEMORY: { // Cmd5
            ReadMemoryReq req_;
            connRead(&req_, sizeof(req_));

            if (req_.Size > 0x1000)
                // Too big read.
                fatalSimple(222 | (5 << 9));

            static u8 page[0x1000];
            rc = svcReadDebugProcessMemory(page, req_.DbgHandle, req_.Addr, req_.Size);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = req_.Size;
                resp.Data = &page[0];
            }

            sendResponse(resp);
            break;
        }
        case REQ_CONTINUE_DBGEVENT: { // Cmd6
            ContinueDbgEventReq req_;
            connRead(&req_, sizeof(req_));

            rc = svcContinueDebugEvent(req_.DbgHandle, req_.Flags, req_.ThreadId);
            resp.Result = rc;

            sendResponse(resp);
            break;
        }
        case REQ_GET_THREADCONTEXT: { // Cmd7
            GetThreadContextReq   req_;
            GetThreadContextResp  resp_;
            connRead(&req_, sizeof(req_));

            rc = svcGetDebugThreadContext(&resp_.Out[0], req_.DbgHandle, req_.ThreadId, req_.Flags);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = sizeof(resp_);
                resp.Data = &resp_;
            }

            sendResponse(resp);
            break;
        }
        case REQ_BREAK_PROCESS: { // Cmd8
            BreakProcessReq req_;
            connRead(&req_, sizeof(req_));

            rc = svcBreakDebugProcess(req_.DbgHandle);
            resp.Result = rc;

            sendResponse(resp);
            break;
        }
        case REQ_WRITEMEMORY: { // Cmd9
            WriteMemoryReq req_;
            connRead(&req_, sizeof(req_));

            rc = svcWriteDebugProcessMemory(req_.DbgHandle, (void*)&req_.Value, req_.Addr, 4);
            resp.Result = rc;

            sendResponse(resp);
            break;
        }
        case REQ_LISTENAPPLAUNCH: { // Cmd10
            Handle h;
            rc = pmdmntEnableDebugForApplication(&h);
            resp.Result = rc;

            if (rc == 0)
                svcCloseHandle(h);

            sendResponse(resp);
            break;
        }
        case REQ_GETAPPPID: { // Cmd11
            GetAppPidResp resp_;

            rc = pmdmntGetApplicationPid(&resp_.Pid);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = sizeof(resp_);
                resp.Data = &resp_;
            }

            sendResponse(resp);
            break;
        }
        case REQ_START_PROCESS: { // Cmd12
            StartProcessReq req_;
            connRead(&req_, sizeof(req_));

            rc = pmdmntStartProcess(req_.Pid);
            resp.Result = rc;

            sendResponse(resp);
            break;
        }
        case REQ_GET_TITLE_PID: { // Cmd13
            GetTitlePidReq   req_;
            GetTitlePidResp  resp_;
            connRead(&req_, sizeof(req_));

            rc = pmdmntGetTitlePid(&resp_.Pid, req_.TitleId);
            resp.Result = rc;

            if (rc == 0) {
                resp.LenBytes = sizeof(resp_);
                resp.Data = &resp_;
            }

            sendResponse(resp);
            break;
        }
        default: {
            // Unknown request.
            //fatalSimple(222 | (2 << 9));
            return 0;
        }
    }

    return 1;
}

int menu = 1;

void run_thread(void* arg) {
    if (menu == 1) {
        int serv = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (serv < 0) {
            fprint("socket() failed! Error: %s", strerror(errno));
        }
        struct sockaddr_in addr;
        addr.sin_len = sizeof(struct sockaddr_in);
        addr.sin_family = AF_INET;
        addr.sin_port = htons(0xDEAF);
        addr.sin_addr.s_addr = INADDR_ANY;
        memset(&addr.sin_zero, 0, 8);
        if (bind(serv, (struct sockaddr*)&addr, sizeof(struct sockaddr_in)) < 0) {
            fprint("bind() failed! Error: %s", strerror(errno));
            while (appletMainLoop() && HOMEBREW_LAUNCHER) {
                print(1, 3, 0, "bind() failed! Error: %s", strerror(errno));
                gfxFlushBuffers();
                gfxSwapBuffers();
                gfxWaitForVsync();
            }

            // file log these
        }

        if (listen(serv, 4) < 0) {
            fprint("listen() failed! Error: %s", strerror(errno));
            while (appletMainLoop() && HOMEBREW_LAUNCHER) {
                print(1, 3, 0, "listen() failed! Error: %s", strerror(errno));
                gfxFlushBuffers();
                gfxSwapBuffers();
                gfxWaitForVsync();
            }
        }

        print(1, 3, 0, "Listening...");
        if (HOMEBREW_LAUNCHER) {
            gfxFlushBuffers();
            gfxSwapBuffers();
            gfxWaitForVsync();
        }

        struct sockaddr_in caddr;
        socklen_t caddrsize = sizeof(caddr);
        sock = accept(serv, (struct sockaddr*)&caddr, &caddrsize);
        if (sock < 0) {
            fprint("accept() failed! Error: %s", strerror(errno));
            while (appletMainLoop() && HOMEBREW_LAUNCHER) {
                print(1, 3, 0, "accept() failed! Error: %s", strerror(errno));
                gfxFlushBuffers();
                gfxSwapBuffers();
                gfxWaitForVsync();
            }
        }

        print(1, 1, 0, "Accepted!!!");
        if (HOMEBREW_LAUNCHER) {
            gfxFlushBuffers();
            gfxSwapBuffers();
            gfxWaitForVsync();
        }

        connectionType = ConnectionType::TCP;
    }

    if (menu < 2) {
        clear();
        do {
            print(40, 1, 1, "Komodo");

            if (menu == 0)
                print(1, 1, 0, "Connected over USB");
            else if (menu == 1)
                print(1, 1, 0, "Connected over TCP/IP");

            if (HOMEBREW_LAUNCHER) {
                gfxFlushBuffers();
                gfxSwapBuffers();
                gfxWaitForVsync();
            }
        } while (handleCommand());
    }
}

int main(int argc, char *argv[]) {
    Result rc;

    if (HOMEBREW_LAUNCHER) {
        gfxInitDefault();
        consoleInit(NULL);
    }

    rc = pmdmntInitialize();
    // Failed to get PM debug interface.
    if (rc)
        fatalSimple(222 | (6 << 9));

    if (HOMEBREW_LAUNCHER) {
        while (appletMainLoop()) {
            hidScanInput();

            u64 hid;
            //hid = hidKeysHeld(CONTROLLER_P1_AUTO);
            hid = hidKeysDown(CONTROLLER_P1_AUTO);
            if (hid & KEY_MINUS) break; // break in order to return to hbmenu

            print(40, 1, 1, "Komodo");

            print(18, 10, 0, "  Connect over USB");
            print(18, 11, 0, "  Connect over TCP/IP");
            print(18, 12, 0, "  Exit");

            print(18, 10 + menu, 0, ">");

            print(1, 43, 0, "Press A to select thing.");

            if ((hid & KEY_DUP) || (hid & KEY_LSTICK_UP) || (hid & KEY_RSTICK_UP))
                menu--;
            if ((hid & KEY_DDOWN) || (hid & KEY_LSTICK_DOWN) || (hid & KEY_RSTICK_DOWN))
                menu++;

            if (menu < 0)
                menu = 0;
            if (menu > 2)
                menu = 2;

            if (hid & KEY_A) {
                break;
            }
            if (hid & KEY_B) {
                menu = 2;
                break;
            }

            gfxFlushBuffers();
            gfxSwapBuffers();
            gfxWaitForVsync();
        }
    }

    if (menu == 0) {
        // Initialize USB
        rc = usbCommsInitializeEx(&usb_interface, USB_CLASS_VENDOR_SPEC, USB_CLASS_VENDOR_SPEC, USB_CLASS_APPLICATION);
        if (rc)
            fatalSimple(rc);

        connectionType = ConnectionType::USB;
    }
    else if (menu == 1) {
        // Initialize TCP/IP
        socketInitializeDefault();
    }

    static Thread conn_thread;

    if (HOMEBREW_LAUNCHER) {
        run_thread(NULL);
    }
    else {

    	rc = threadCreate(&conn_thread, run_thread, 0, 0x4000, 28, -2);
    	if (R_FAILED(rc)) {
            pmdmntExit();
            gfxExit();
            return 0;
        }

    	rc = threadStart(&conn_thread);
    	if (R_FAILED(rc)) {
            pmdmntExit();
            gfxExit();
            return 0;
        }

        //run_thread(NULL);
    }

    threadWaitForExit(&conn_thread);

    if (menu == 0) {
        usbCommsExitEx(usb_interface);
    }
    else if (menu == 1) {
        close(sock);
        socketExit();
    }

    if (HOMEBREW_LAUNCHER) {
        pmdmntExit();
        gfxExit();
    }
    return 0;
}
