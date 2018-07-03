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

int main(int argc, char *argv[]) {
    time_t unixTime = time(NULL);
    struct tm* timeStruct = gmtime((const time_t *)&unixTime);//Gets UTC time. Currently localtime() will also return UTC (timezones not supported).

    int hours = timeStruct->tm_hour;
    int minutes = timeStruct->tm_min;
    int seconds = timeStruct->tm_sec;

    FILE* fp = fopen("file.txt","w");
    fprintf(fp, "%02i:%02i:%02i Greetings...", hours, minutes, seconds);
    fclose(fp);
    return 0;
}
