using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using log4net.Core;
using log4net.Layout;

namespace Log4Slack;

internal static class Extensions
{
	public static string Expand(this string text)
	{
		return (text != null) ? Environment.ExpandEnvironmentVariables(text) : null;
	}

	public static IEnumerable<string> SplitOn(this string text, int numChars)
	{
		Regex regex = new Regex($"(?<line>.{{1,{numChars}}})([\\r\\n]|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		return from m in regex.Matches(text).OfType<Match>()
			select m.Groups["line"].Value;
	}

	public static string FormatString(this ILayout layout, LoggingEvent loggingEvent)
	{
		using StringWriter stringWriter = new StringWriter();
		layout.Format((TextWriter)stringWriter, loggingEvent);
		return stringWriter.ToString();
	}
}
