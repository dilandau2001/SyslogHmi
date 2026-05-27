namespace SyslogHmi.Models
{
    public enum SyslogFacility
    {
        Kernel = 0,
        User = 1,
        Mail = 2,
        Daemon = 3,
        Auth = 4,
        Syslog = 5,
        Lpr = 6,
        News = 7,
        Uucp = 8,
        Cron = 9,
        AuthPriv = 10,
        Ftp = 11,
        Ntp = 12,
        LogAudit = 13,
        LogAlert = 14,
        ClockDaemon = 15,
        Local0 = 16,
        Local1 = 17,
        Local2 = 18,
        Local3 = 19,
        Local4 = 20,
        Local5 = 21,
        Local6 = 22,
        Local7 = 23,
        Unknown = -1
    }

    public enum SyslogSeverity
    {
        Emergency = 0,
        Alert = 1,
        Critical = 2,
        Error = 3,
        Warning = 4,
        Notice = 5,
        Info = 6,
        Debug = 7,
        Unknown = -1
    }
}
