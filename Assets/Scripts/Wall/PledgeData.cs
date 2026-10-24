using System;
using System.Collections.Generic;

[Serializable]
public class PledgeEntry
{
    public string visitorName;
    public string emailId;
    public string employeeId;   // legacy: older saves stored an employee ID
    public string pledgeText;   // legacy: older saves stored a typed message
    public string imageBase64;
    public string timestamp;
}

[Serializable]
public class PledgeDatabase
{
    public List<PledgeEntry> pledges = new List<PledgeEntry>();
}
