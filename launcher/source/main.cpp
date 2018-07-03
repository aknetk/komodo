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
#include <errno.h>
#include <switch.h>

void clear() {
    for (int i = 1; i <= 45; i++)
        printf("\x1b[%d;1H%s", i, "                                                                                ");
}
void print(char x, char y, char align, const char* string, ...) {
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

Service komodo;

struct SegmentHeader {
	uint32_t FileOffset;
	uint32_t Size;
};
struct NROHeader {
	uint32_t Unused;
	uint32_t ModOffset;
	uint32_t Padding1[2];
	uint32_t Magic; // NRO0
	uint32_t FormatVersion;
	uint32_t Filesize;
	uint32_t Flags;
	SegmentHeader Segments[3];
	uint32_t BSSSize;
};

struct ProcessInfo {
    u8 name[12];
    u32 process_category;
    u64 title_id;
    u64 code_addr;
    u32 code_num_pages;
    u32 process_flags;
    Handle reslimit_h;
    u32 system_resource_num_pages;
};
struct AddressSpaceInfo {
    u64 heap_base;
    u64 heap_size;
    u64 heap_end;
    u64 map_base;
    u64 map_size;
    u64 map_end;
    u64 addspace_base;
    u64 addspace_size;
    u64 addspace_end;
};

#define printOut(words) { do { hidScanInput(); print(1, 3, 0, words); } while ((hidKeysDown(CONTROLLER_P1_AUTO) & KEY_A) == 0); }

#define printOutImmediate(words) { do { hidScanInput(); print(1, 3, 0, words); gfxFlushBuffers(); gfxSwapBuffers(); gfxWaitForVsync(); } while ((hidKeysDown(CONTROLLER_P1_AUTO) & KEY_A) == 0); }

void __appInit(void) {
    Result rc;

    rc = smInitialize();
    if (R_FAILED(rc))
        fatalSimple(MAKERESULT(Module_Libnx, LibnxError_InitFail_SM));

    rc = fsInitialize();
    if (R_FAILED(rc))
        fatalSimple(MAKERESULT(Module_Libnx, LibnxError_InitFail_FS));

    rc = pmshellInitialize();
    if (R_FAILED(rc))
        fatalSimple(0xCAFE << 4 | 1);
}

void __appExit(void) {
    /* Cleanup services. */
    pmshellExit();
    fsExit();
    smExit();
}

const char* errorTexts[20] = {
    "Unknown error",
    "Memory alloc failed.",
    "Entire read failed.",
    "Invalid NRO. (NRO too small)",
    "Invalid NRO. (incorrect magic)",
    "Invalid NRO. (incorrect filesize)",
    "Could not create resource limit.",
    "Could not create process.",
    "Could not map memory.",
    "Could not set permissions.",
    "Could not start process.",
    "Could not set resource limit for Memory.",
    "Could not set resource limit for Threads.",
    "Could not set resource limit for Events.",
    "Could not set resource limit for TransferMemories.",
    "Could not set resource limit for Sessions.",
    "Unknown error",
    "Unknown error",
    "Unknown error",
    "Unknown error",
};

int errno2 = 0;
static uint8_t* nro;

u64 load_base = 0x7100000000;

Result GetAddressSpaceInfo(AddressSpaceInfo *out, Handle process_h) {
    Result rc;
    if (R_FAILED((rc = svcGetInfo(&out->heap_base, 4, CUR_PROCESS_HANDLE, 0)))) {
        return rc;
    }
    if (R_FAILED((rc = svcGetInfo(&out->heap_size, 5, CUR_PROCESS_HANDLE, 0)))) {
        return rc;
    }
    if (R_FAILED((rc = svcGetInfo(&out->map_base, 2, CUR_PROCESS_HANDLE, 0)))) {
        return rc;
    }
    if (R_FAILED((rc = svcGetInfo(&out->map_size, 3, CUR_PROCESS_HANDLE, 0)))) {
        return rc;
    }
    if (R_FAILED((rc = svcGetInfo(&out->addspace_base, 12, CUR_PROCESS_HANDLE, 0)))) {
        return rc;
    }
    if (R_FAILED((rc = svcGetInfo(&out->addspace_size, 13, CUR_PROCESS_HANDLE, 0)))) {
        return rc;
    }
    out->heap_end = out->heap_base + out->heap_size;
    out->map_end = out->map_base + out->map_size;
    out->addspace_end = out->addspace_base + out->addspace_size;
    return 0;
}
Result LocateSpaceForMap(u64 *out, u64 out_size) {
    MemoryInfo mem_info = {0};
    AddressSpaceInfo address_space = {0};
    u32 page_info = 0;
    u64 cur_base = 0, cur_end = 0;
    Result rc;

    if (R_FAILED((rc = GetAddressSpaceInfo(&address_space, CUR_PROCESS_HANDLE)))) {
        return rc;
    }

    cur_base = address_space.addspace_base;

    rc = 0xD001;
    cur_end = cur_base + out_size;
    if (cur_end <= cur_base) {
        return rc;
    }

    while (true) {
        if (address_space.heap_size && (address_space.heap_base <= cur_end - 1 && cur_base <= address_space.heap_end - 1)) {
            /* If we overlap the heap region, go to the end of the heap region. */
            if (cur_base == address_space.heap_end) {
                return rc;
            }
            cur_base = address_space.heap_end;
        } else if (address_space.map_size && (address_space.map_base <= cur_end - 1 && cur_base <= address_space.map_end - 1)) {
            /* If we overlap the map region, go to the end of the map region. */
            if (cur_base == address_space.map_end) {
                return rc;
            }
            cur_base = address_space.map_end;
        } else {
            if (R_FAILED(svcQueryMemory(&mem_info, &page_info, cur_base))) {
                /* TODO: panic. */
            }
            if (mem_info.type == 0 && mem_info.addr - cur_base + mem_info.size >= out_size) {
                *out = cur_base;
                return 0x0;
            }
            if (mem_info.addr + mem_info.size <= cur_base) {
                return rc;
            }
            cur_base = mem_info.addr + mem_info.size;
            if (cur_base >= address_space.addspace_end) {
                return rc;
            }
        }
        cur_end = cur_base + out_size;
        if (cur_base + out_size <= cur_base) {
            return rc;
        }
    }
}

int buildProcess(const char* name, const char* filename) {
    Result rc;

    FILE* fp = fopen(filename, "rb");

    fseek(fp, 0, SEEK_END);
    size_t nro_size = ftell(fp);
    rewind(fp);

    nro = (uint8_t*)calloc(1, nro_size + 1);
    if (!nro) {
        fclose(fp);
        fputs("memory alloc fails", stderr);
        return 1;
    }
    if (fread(nro, nro_size, 1, fp) != 1) {
        fclose(fp);
        free(nro);
        fputs("entire read fails", stderr);
        return 2;
    }
    fclose(fp);





    if (nro_size < sizeof(NROHeader)) {
        free(nro);
        return 3; // invalid NRO
    }

    NROHeader* nro_header = (NROHeader*)nro;
    if (nro_header->Magic != 0x304F524E) {
        free(nro);
        return 4; // invalid NRO
    }

    if (nro_size < nro_header->Filesize) {
        free(nro);
        return 5; // invalid NRO
    }

    u64 base;

    u64 load_addr[3];
    u64 total_size = 0;

    u32 caps[11] = {
        0b00000011000000000111001110110111,
        0b00011111111111111111111111001111,
        0b00111111111111111111111111101111,
        0b01000111111001100000011111101111,
        0b01111111111111111111111111101111,
        0b10011111111101111111111111101111,
        0b10100000000000000001111111101111,
        0b00000000000000001001111111111111,
        0b00000000000110000011111111111111,
        0b00000010000000000111111111111111,
        0b00000000000001101111111111111111,
	};

    for (int i = 0; i < 3; i++) {
        load_addr[i] = total_size;

        u64 size = nro_header->Segments[i].Size;
        if (i == 2)
            size += nro_header->BSSSize;
        total_size += size;
    }

    ProcessInfo process_info;
    process_info.process_category = 0;
	process_info.title_id = 0x014000000000100D; // creport
	process_info.process_flags = 0b10110111; // ASLR, 39-bit address space, AArch64, bit4 (?)
	process_info.system_resource_num_pages = 0;

    u32 addspace_type = process_info.process_flags;

    u64 addspace_start, addspace_size;
    if (kernelAbove200()) {
        switch (addspace_type & 0xE) {
            case 0:
            case 4:
                addspace_start = 0x200000ULL;
                addspace_size = 0x3FE00000ULL;
                break;
            case 2:
                addspace_start = 0x8000000ULL;
                addspace_size = 0x78000000ULL;
                break;
            case 6:
                addspace_start = 0x8000000ULL;
                addspace_size = 0x7FF8000000ULL;
                break;
            default:
                /* TODO: Panic. */
                return 0xD001;
        }
    } else {
        if (addspace_type & 2) {
            addspace_start = 0x8000000ULL;
            addspace_size = 0x78000000ULL;
        } else {
            addspace_start = 0x200000ULL;
            addspace_size = 0x3FE00000ULL;
        }
    }

    u64 aslr_slide = 0;
    if (addspace_type & 0x20) {
        aslr_slide = (randomGet64() % ((addspace_size - total_size) >> 21)) << 21;
    }

    base = addspace_start + aslr_slide;

    Handle resource_limit;
    rc = svcCreateResourceLimit(&resource_limit);
    if (R_FAILED(rc)) {
        free(nro);
        return 6; // couldn't create resource limit
    }
    rc = svcSetResourceLimitLimitValue(resource_limit, LimitableResource_Memory, 6 * 1024 * 1024);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 11; // couldn't set resource limit
    }
    rc = svcSetResourceLimitLimitValue(resource_limit, LimitableResource_Threads, 256);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 12; // couldn't set resource limit
    }
    rc = svcSetResourceLimitLimitValue(resource_limit, LimitableResource_Events, 256);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 13; // couldn't set resource limit
    }
    rc = svcSetResourceLimitLimitValue(resource_limit, LimitableResource_TransferMemories, 256);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 14; // couldn't set resource limit
    }
    rc = svcSetResourceLimitLimitValue(resource_limit, LimitableResource_Sessions, 256);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 15; // couldn't set resource limit
    }


    process_info.code_addr = base;
	process_info.reslimit_h = resource_limit;
	process_info.code_num_pages = total_size + 0xFFF;
    process_info.code_num_pages >>= 12;

    strncpy((char*)process_info.name, name, sizeof(process_info).name - 1);
	process_info.name[sizeof(process_info).name - 1] = 0;


    /* Call svcCreateProcess(). */
    Handle komodo_process;
    rc = svcCreateProcess(&komodo_process, &process_info, caps, 11);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 7; // couldn't create process
    }

    u64 map_addr = load_base;

    /* from what I can guess

    svcMapProcessMemory keeps giving the "Invalid address" error code (0xCC01)
    no matter what i do, changing the dest address changes nothing
    perhaps the nro is an invalid address?

    //*/

    // LoadNsosIntoProcessMemory

    u64 try_address;
    if (R_FAILED(rc = LocateSpaceForMap(&try_address, total_size))) {
        errno2 = rc;
        free(nro);
        return 18;
    }

    rc = svcMapProcessMemory((void*)try_address, komodo_process, base, total_size);
    if (R_FAILED(rc)) {
        errno2 = rc;
        free(nro);
        return 8; // couldn't map memory
    }

    memcpy((u8*)try_address, nro, nro_size);

    /*for (int i = 0; i < 3; i++) {
        rc = svcMapProcessMemory((void*)(load_base + (load_addr[i] - load_base)), komodo_process, (u64)(nro + nro_header->Segments[i].FileOffset), (u64)nro_header->Segments[i].Size);
        if (R_FAILED(rc)) {
            errno2 = rc;
            free(nro);
            return 8; // couldn't map memory
        }
        //memcpy(load_base + (load_addr[i] - load_base), nro + nro_header->Segments[i].FileOffset, nro_header->Segments[i].Size);
    }*/

    u32 perms[3] = { 5, 1, 3 }; // RX, R, RW

    for (int i = 0; i < 3; i++) {
        u64 size = nro_header->Segments[i].Size;
        if (i == 2)
            size += nro_header->BSSSize;

        rc = svcSetProcessMemoryPermission(komodo_process, base + load_addr[i], size, perms[i]);
        if (R_FAILED(rc)) {
            errno2 = rc;
            free(nro);
            return 9; // couldn't set permissions
        }
    }
    free(nro);

    printOutImmediate("Press A to start?");

    rc = svcStartProcess(komodo_process, 58, 0, 0x10000);
    if (R_FAILED(rc)) {
        errno2 = rc;
        return 10; // couldn't start process
    }

    printOutImmediate("Started?          ");
    printOutImmediate("Really?  ");

    return 0;
}

int rreor = -1;
int menu = 0;

int main(int argc, char *argv[]) {
    gfxInitDefault();
    consoleInit(NULL);

    while (appletMainLoop()) {
        hidScanInput();

        u64 hid;
        //hid = hidKeysHeld(CONTROLLER_P1_AUTO);
        hid = hidKeysDown(CONTROLLER_P1_AUTO);
        if (hid & KEY_MINUS) break; // break in order to return to hbmenu

        print(40, 1, 2, "Komodo Launcher");

        print(18, 10, 0, "  Launch Komodo");
        print(18, 11, 0, "  Uhhhhhh");
        print(18, 12, 0, "  Exit");

        print(18, 10 + menu, 0, ">");

        print(1, 43, 0, "Press A to select thing. %X", load_base);

        if (rreor > 0) {
            print(1, 40, 0, "Error: %s [%X]", errorTexts[rreor], errno2);
        }
        else if (rreor > -1) {
            print(1, 40, 0, "Successfully launched!");
        }

        if ((hid & KEY_DUP) || (hid & KEY_LSTICK_UP) || (hid & KEY_RSTICK_UP))
            menu--;
        if ((hid & KEY_DDOWN) || (hid & KEY_LSTICK_DOWN) || (hid & KEY_RSTICK_DOWN))
            menu++;

        if (menu < 0)
            menu = 0;
        if (menu > 2)
            menu = 2;

        if (hid & KEY_A) {
            if (menu == 0) {
                rreor = buildProcess("komodo", "testservice.nro");
                if (rreor == 0) {
                    gfxExit();
                    return 0;
                }
            }
            else if (menu == 2) {
                break;
            }
        }
        if (hid & KEY_B) {
            break;
        }

        gfxFlushBuffers();
        gfxSwapBuffers();
        gfxWaitForVsync();
    }

    gfxExit();
    return 0;
}
