# Setting up Azure Log Analytics Access

This document describes how to connect the `guardian2LA` container to a Log Analytics instance.

## Getting the Workspace ID and Workspace Key

The workspace ID and key are provided on the same page as follows:
* Navigate to the workspace blage (in this case [platform-demos-workspace](https://portal.azure.com/#@42css.onmicrosoft.com/resource/subscriptions/4effd691-89f5-468e-ac5f-1e1002e3eb2a/resourceGroups/customer-pocs/providers/Microsoft.OperationalInsights/workspaces/platform-demos-workspace/Overview))
* On the left hand navigation bar select Settings | Agents management
* This should reveal a panel with the workspace ID and keys as shown:

![Setting up Azure Log Analytics Access](SettingUpAzureLogAnalyticsAccess.png)

Copy the ID and one of the keys (either will work), and provide these as environment variables to the `guardian2LA` container.

## Regenerating the Workspace Key

If the key is compromised it is possible to regenerate the key using the Regenerate button provided which will revoke all instances of the previous sessions using the old key. It is probably wisest to use the Secondary key for `guardian2LA` container to keep it separate from other agents if used at all.