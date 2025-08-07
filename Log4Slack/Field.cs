using System.Runtime.Serialization;

namespace Log4Slack;

[DataContract]
public record Field(
    [field: DataMember(Name = "title")] string Title,
    [field: DataMember(Name = "value")] string Value,
    [field: DataMember(Name = "short")] bool Short = false);