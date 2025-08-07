using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Log4Slack;

[DataContract]
public record Payload(
	[field: DataMember(Name = "channel")] string Channel,
    [field: DataMember(Name="username")] string Username,
    [field: DataMember(Name = "icon_url")]string IconUrl,
    [field: DataMember(Name = "icon_emoji")] string IconEmoji,
    [field: DataMember(Name = "text")] string Text,
    [field: DataMember(Name = "attachments")] List<Attachment> Attachments,
    [field: DataMember(Name = "link_names")] int LinkNames);