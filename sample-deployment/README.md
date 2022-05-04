# Sample HTTPbin Deployment

You can use the Docker compose file available in this sample to launch a 42Crunch API Firewall protecting a subset of the sample [httpbin](https://httpbin.org) API. 

The compose file launches: 

- The 42Crunch API Firewall
- The httpbin API
- The Azure logs analytics forwarder

This sample assumes that you have access to an Azure Log Analytics workspace, as well as the workspace ID and key to push data into that workspace.

## Running the sample

In order to run the sample, you need to:

1. Export the FW2LA_WORKSPACE_ID env variable: `export FW2LA_WORKSPACE_ID=<your_workspace_id>` 

2. Export the FW2LA_WORKSPACE_KEY env variable: `export FW2LA_WORKSPACE_ID=<your_workspace_key>`

3. Run the docker compose command: `docker compose up`

   The API Firewall will be exposed on http://localhost:8080- It was configured to use HTTP instead of HTTPs (which is the default) for testing purposes.

## Testing the firewall

You can import the `httpbin-spec.json` OpenAPI file in your favorite testing tool, such as Postman and invoke the httpbin endpoints exposed. Some will work fine, others will fail either on request or response validation (this is done on purpose to generate different types of log entries).

You can also invoke the API with different verbs and data formats to trigger errors.

If the Logs Analytics connection is set up properly, you should see logs appearing a new table under your log analytics workspace.

