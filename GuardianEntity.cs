using System;
using System.Net;
using Newtonsoft.Json.Linq;
using static fwlogs2loganalytics.Program;

namespace fwlogs2loganalytics
{
    public class GuardianEntity
    {
        // Useful documentation here: https://docs.42crunch.com/latest/content/extras/api_firewall_variables.htm

        public long LogType { get; set; }

        // Record UID 
        public string UUID { get; set; }

        // The API Firewall/Guardian instance name
        // Logs are written here:  /opt/guardian/logs/${GUARDIAN_INSTANCE_NAME}
        public string Instance_Name { get; set; }

        // The Guardian node name
        public string Node_Name { get; set; }

        // Timestamp as DateTime
        public DateTime Timestamp { get; set; }

        // The API ID
        public string API_ID { get; set; }

        public string API_Name { get; set; }

        public bool Non_blocking_mode { get; set; }

        public string Source_IP { get; set; }

        public double Source_Port { get; set; }

        public string Destination_IP { get; set; }

        public double Destination_Port { get; set; }

        public string Protocol { get; set; }

        public string Hostname { get; set; }

        public string URI_Path { get; set; }

        public string Method { get; set; }

        public double Status { get; set; }

        public string Query { get; set; }

        public string Request_Header { get; set; }

        public string Response_Header { get; set; }

        public string Errors { get; set; }

        public string Error_Message { get; set; }

        public string Error_Step { get; set; }

        public string Tags { get; set; }

        public GuardianEntity(GuardianLogType logType, string UUID, string instance_Name, string node_Name, long timestamp, string API_ID, string API_Name, bool non_blocking_mode, string source_IP, uint source_Port, string destination_IP, uint destination_Port, string protocol, string hostname, string URI_Path, string method, uint status, string query = null, string request_Header = null, string response_Header = null, string errors = null, string tags = null)
        {
            const string NA_STR = "n/a";
            // The epoch we're given is in microseconds
            DateTimeOffset dto = DateTimeOffset.FromUnixTimeMilliseconds(timestamp/1000);

            // Get the details on the 'errors' if we have
            var error = JArray.Parse(errors);

            if (error.Count != 0)
            {
                Error_Message = error.Last["message"].Value<string>();
                Error_Step = error.Last["step"].Value<string>();
            }
            else
                errors = null;
            
            if (API_ID.Contains(NA_STR))
                API_ID = null;

            if (API_Name.Contains(NA_STR))
                API_Name = null;

            // Store the values
            this.LogType = (long)logType;
            this.UUID = UUID;
            this.Instance_Name = instance_Name;
            this.Node_Name = node_Name;
            this.Timestamp =  dto.UtcDateTime;
            this.API_ID = API_ID;
            this.API_Name = API_Name;
            this.Non_blocking_mode = non_blocking_mode;
            this.Source_IP = source_IP;
            this.Source_Port = source_Port;
            this.Destination_IP = destination_IP;
            this.Destination_Port = destination_Port;
            this.Protocol = protocol;
            this.Hostname = hostname;
            this.URI_Path = URI_Path;
            this.Method = method;
            this.Status = status;
            this.Query = query;
            this.Request_Header = request_Header;
            this.Response_Header = response_Header;
            this.Errors = errors;
            this.Tags = tags;
        }

        public override string ToString()
        {
            return $"GuardianEntity() -> UUID: {UUID}, Instance_Name: {Instance_Name}, Node_Name: {Node_Name}, API_ID: {API_ID}, API_Name: {API_Name}, Timestamp: {Timestamp}, Source_IP: {Source_IP}, Destination_IP: {Destination_IP}, Hostname: {Hostname}, URI_Path: {URI_Path}, Method: {Method}, Status: {Status}, Non_blocking_mode: {Non_blocking_mode}".ToString();
        }
    }
}