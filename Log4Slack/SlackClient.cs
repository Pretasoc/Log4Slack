using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using log4net.Util;

namespace Log4Slack
{
    public class SlackClient : IDisposable
    {
        private readonly string _channel;

        private readonly HttpClient _client = new HttpClient();

        private readonly string _iconEmoji;

        private readonly string _iconUrl;
        private readonly Uri _uri;

        private readonly string _username;

        public SlackClient(string urlWithAccessToken)
        {
            _uri = new Uri(urlWithAccessToken);
        }

        public SlackClient(
            string urlWithAccessToken,
            string username,
            string channel,
            string iconUrl = null,
            string iconEmoji = null)
        {
            _uri = new Uri(urlWithAccessToken);
            _username = username;
            _channel = channel;
            _iconUrl = iconUrl;
            _iconEmoji = iconEmoji;
            ServicePointManager.Expect100Continue = true;
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        public async Task PostMessageAsync(
            string text,
            string proxyAddress,
            string username = null,
            string channel = null,
            string iconUrl = null,
            string iconEmoji = null,
            List<Attachment> attachments = null,
            bool linknames = false)
        {
            Payload payload = BuildPayload(text, username, channel, iconUrl, iconEmoji, attachments, linknames);
            await PostPayloadAsync(payload, proxyAddress).ConfigureAwait(false);
        }

        protected virtual async Task PostPayloadAsync(string json, string proxyAddress)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                string payload = $"payload={Uri.EscapeDataString(json)}";
                LogLog.Debug(typeof(SlackClient), payload);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

                HttpContent content = new ByteArrayContent(payloadBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                HttpResponseMessage response = await _client.PostAsync(_uri, content).ConfigureAwait(false);

                if (LogLog.IsDebugEnabled)
                {
                    LogLog.Debug(typeof(SlackClient), "Slack response: " + response.StatusCode);
                    using Stream responseStream = await response.Content.ReadAsStreamAsync();
                    using StreamReader reader = new StreamReader(responseStream);
                    string? message = await reader.ReadToEndAsync().ConfigureAwait(false);
                    LogLog.Debug(typeof(SlackClient), "Slack response: " + message);
                }
            }
            catch (Exception e)
            {
                LogLog.Error(typeof(SlackClient), "Unable to send message to Slack", e);
            }
        }

        private Payload BuildPayload(
            string text,
            string? username,
            string? channel,
            string? iconUrl,
            string? iconEmoji,
            List<Attachment>? attachments = null,
            bool linkNames = false)
        {
            return new Payload(
                string.IsNullOrEmpty(channel) ? _channel : channel!,
                string.IsNullOrEmpty(username) ? _username : username!,
                string.IsNullOrEmpty(iconUrl) ? _iconUrl : iconUrl!,
                string.IsNullOrEmpty(iconEmoji) ? _iconEmoji : iconEmoji!,
                text,
                attachments,
                Convert.ToInt32(linkNames)
            );
        }

        private static string JsonSerializeObject(object obj)
        {
            DataContractJsonSerializer dataContractJsonSerializer = new DataContractJsonSerializer(obj.GetType());
            using MemoryStream memoryStream = new MemoryStream();
            dataContractJsonSerializer.WriteObject(memoryStream, obj);
            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }

        private async Task PostPayloadAsync(Payload payload, string proxyAddress)
        {
            string json = JsonSerializeObject(payload);
            await PostPayloadAsync(json, proxyAddress).ConfigureAwait(false);
        }
    }
}
