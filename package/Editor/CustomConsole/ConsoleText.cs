﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Needle.Demystify
{
	internal static class ConsoleText
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			cachedInfo.Clear();
			
			// clear cache when colors change
			DemystifyProjectSettings.ColorSettingsChanged += () =>
			{
				cachedInfo.Clear();
			};
		}
		
		private static readonly LogEntry tempEntry = new LogEntry();
		

		private static readonly string[] onlyUseMethodNameFromLinesWithout = new[]
		{
			"UnityEngine.UnitySynchronizationContext",
			"UnityEngine.Debug",
			"UnityEngine.Logger",
			"UnityEngine.DebugLogHandler",
			"System.Runtime.CompilerServices"
		};

		private static bool TryGetMethodName(string message, out string methodName)
		{
			using (new ProfilerMarker("ConsoleList.ParseMethodName").Auto())
			{
				using (var rd = new StringReader(message))
				{
					var linesRead = 0;
					while (true)
					{
						var line = rd.ReadLine(); 
						if (line == null) break;
						if (onlyUseMethodNameFromLinesWithout.Any(line.Contains)) continue; 
						if (!line.Contains(".cs")) continue;
						Match match;
						using (new ProfilerMarker("Regex").Auto())
							match = Regex.Match(line, @".*?(\..*?){0,}[\.\:](?<method_name>.*?)\(.*\.cs(:\d{1,})?", RegexOptions.Compiled | RegexOptions.ExplicitCapture); 
						using (new ProfilerMarker("Handle Match").Auto())
						{
							// var match = matches[i];
							var group = match.Groups["method_name"];
							if (group.Success)
							{
								methodName = group.Value.Trim();
								return true;
							}
						}

						linesRead += 1;
						if (linesRead > 15) break;
					}
				}

				methodName = null;
				return false;
			}
		}

		private static readonly Dictionary<string, string> cachedInfo = new Dictionary<string, string>();

		// called from console list with current list view element and console text
		internal static void ModifyText(ListViewElement element, ref string text)
		{
			// var rect = element.position;
			// GUI.DrawTexture(rect, Texture2D.whiteTexture);//, ScaleMode.StretchToFill, true, 1, Color.red, Vector4.one, Vector4.zero);
			
			using (new ProfilerMarker("ConsoleList.ModifyText").Auto())
			{
				if (!DemystifySettings.instance.ShowFileName) return;
				
				var key = text;
				if (cachedInfo.ContainsKey(key))
				{
					text = cachedInfo[key];
					return;
				}

				if (LogEntries.GetEntryInternal(element.row, tempEntry))
				{
					var filePath = tempEntry.file;
					if (!string.IsNullOrWhiteSpace(filePath)) // && File.Exists(filePath))
					{
						try
						{
							var fileName = Path.GetFileNameWithoutExtension(filePath);
							const string colorPrefix = "<color=#999999>";
							const string colorPostfix = "</color>";

							var colorKey = fileName;
							var colorMarker = DemystifySettings.instance.ColorMarker;// " ▍";
							if(!string.IsNullOrWhiteSpace(colorMarker))
								LogColor.AddColor(colorKey, ref colorMarker);
							
							string GetText()
							{
								var str = fileName;
								if (TryGetMethodName(tempEntry.message, out var methodName))
								{
									// colorKey += methodName;
									str += "." + methodName; 
								}

								// str = colorPrefix + "[" + str + "]" + colorPostfix;
								// str = "<b>" + str + "</b>";
								// str = "\t" + str;
								str = colorPrefix + str + colorPostfix;// + " |";
								return str;
							}

							var endTimeIndex = text.IndexOf("] ", StringComparison.InvariantCulture);

							// text = element.row.ToString();
							
							// no time:
							if (endTimeIndex == -1)
							{
								// LogColor.AddColor(colorKey, ref text);
								text = $"{colorMarker} {GetText()} {text}";
							}
							// contains time:
							else
							{
								var message = text.Substring(endTimeIndex + 1);
								// LogColor.AddColor(colorKey, ref message);
								text = $"{colorPrefix}{text.Substring(1, endTimeIndex-1)}{colorPostfix} {colorMarker} {GetText()} {message}";
							}

							cachedInfo.Add(key, text);
						}
						catch (ArgumentException)
						{
							// sometimes filepath contains illegal characters and is not actually a path
							cachedInfo.Add(key, text);
						}
						catch (Exception e)
						{
							Debug.LogException(e);
							cachedInfo.Add(key, text);
						}
					}
				}
			}
		}

	}
}