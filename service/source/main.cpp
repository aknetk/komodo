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

bool HOMEBREW_LAUNCHER = true;

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

enum class ConnectionType {
    USB,
    TCP
};

ConnectionType connectionType = ConnectionType::USB;

int sock;

size_t usbRead(void* buffer, size_t size) {
    return usbCommsRead(buffer, size);
}
size_t usbWrite(const void* buffer, size_t size) {
    return usbCommsWrite(buffer, size);
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

int main(int argc, char *argv[]) {
    if (HOMEBREW_LAUNCHER) {
        gfxInitDefault();
        consoleInit(NULL);
    }

    Result rc;

    rc = pmdmntInitialize();
    // Failed to get PM debug interface.
    if (rc)
        fatalSimple(222 | (6 << 9));

    int menu = 1;
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
        rc = usbCommsInitialize();
        if (rc)
            fatalSimple(rc);

        connectionType = ConnectionType::USB;
    }
    else if (menu == 1) {
        // Initialize TCP/IP
        socketInitializeDefault();

        int serv = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        struct sockaddr_in addr;
        addr.sin_len = sizeof(struct sockaddr_in);
        addr.sin_family = AF_INET;
        addr.sin_port = htons(0xDEAD);
        addr.sin_addr.s_addr = INADDR_ANY;
        memset(&addr.sin_zero, 0, 8);
        bind(serv, (struct sockaddr*)&addr, sizeof(struct sockaddr_in));
        listen(serv, 1);

        struct sockaddr_in caddr;
        socklen_t caddrsize;
        sock = accept(serv, (struct sockaddr*)&caddr, &caddrsize);

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

    if (menu == 0) {
        usbCommsExit();
    }
    else if (menu == 1) {
        close(sock);
        socketExit();
    }

    pmdmntExit();
    if (HOMEBREW_LAUNCHER)
        gfxExit();
    return 0;
}
