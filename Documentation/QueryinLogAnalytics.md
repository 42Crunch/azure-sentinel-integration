# Querying Log Analytics

This document provides some basic information regarding performing Log Analytics records containing 42Crunch firewall logs.

## Log Analytics data structure

Each field is suffix with an underscore and letter to indicate the data type as follows:
* _d : Long integer value
* _s : String value
* _g : GUID value
* _t : DateTime value
* _b : Boolean value

The 42Crunch `guardian` firewall data is written to fields using the same names as in the `guardian` log files with the following differences:
* Error_Message_s : this is an extract of the 'message' field of the last record in the 'Errors' array
* Error_Step_s : this is an extract of the 'step' field of the last record in the 'Errors' array

Since two different logs are transmitted to Log Analytics these are differentiated using a field called `LogType_d` as follows:
* 1 = API transaction logs
* 2 = Unknown transaction logs

## Sample queries

Here are a few sample KQL queries for illustration:

**Find all records within the last 30 minutes:**

```
guardian_log_1_CL |
where TimeGenerated >= ago(30m)
```

**Find records in the API log with an error status code within the last 30 minutes:**

```
guardian_log_1_CL |
where TimeGenerated >= ago(40m) |
where LogType_d == 1 |
where Status_d >= 400 and Status_d <= 499
```

**Find all unknown transactions within the last 30 minutes:**

```
guardian_log_1_CL |
where TimeGenerated >= ago(30m) |
where LogType_d == 2
```

## Further information

[KQL cheat sheets](https://techcommunity.microsoft.com/t5/azure-data-explorer/azure-data-explorer-kql-cheat-sheets/ba-p/1057404_)  
[KQL reference](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/)
