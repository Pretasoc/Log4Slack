using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Log4Slack;

[DataContract]
public record Attachment(
    [field: DataMember(Name = "fallback")] string Fallback,
    [field: DataMember(Name = "pretext")] string PreText,
    [field: DataMember(Name = "text")] string Text,
    [field: DataMember(Name = "color")] string Color,
    [field: DataMember(Name = "fields")] List<Field> Fields,
    [field: DataMember(Name = "mrkdwn_in")] List<string> MarkdownIn)
{
    public Attachment(string fallback)
        : this(
            fallback,
            string.Empty,
            string.Empty,
            string.Empty,
            new List<Field>(),
            new List<string>
            {
                "fields"
            })
    {
    }
}