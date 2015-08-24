﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using Cognifide.PowerShell.Commandlets.Interactive.Messages;
using Cognifide.PowerShell.Core.Extensions;
using Cognifide.PowerShell.Core.Settings;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Jobs.AsyncUI;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;

namespace Cognifide.PowerShell.Core.Host
{
    public class ScriptingHostUserInterface : PSHostUserInterface, IHostUISupportsMultipleChoiceSelection
    {
        private readonly ScriptingHostRawUserInterface rawUi;

        public ScriptingHostUserInterface(ApplicationSettings settings)
        {
            rawUi = new ScriptingHostRawUserInterface(settings);
        }

        /// <summary>
        ///     A reference to the PSHost implementation.
        /// </summary>
        public OutputBuffer Output
        {
            get { return rawUi.Output; }
        }

        public override PSHostRawUserInterface RawUI
        {
            get { return rawUi; }
        }

        public override string ReadLine()
        {
            if (JobContext.IsJob)
            {
                JobContext.MessageQueue.PutMessage(new PromptMessage("String", string.Empty));
                var alertresult = JobContext.MessageQueue.GetResult() as string;
                return alertresult;
            }
            throw new NotImplementedException();
        }

        public override SecureString ReadLineAsSecureString()
        {
            if (JobContext.IsJob)
            {
                JobContext.MessageQueue.PutMessage(new PromptMessage("String", string.Empty));
                var alertresult = JobContext.MessageQueue.GetResult() as string;
                var secure = new SecureString();
                foreach (char c in alertresult)
                {
                    secure.AppendChar(c);
                }
            }
            throw new NotImplementedException();
        }

        public override void Write(string value)
        {
            var lastline = Output[Output.Count - 1];
            if (!lastline.Terminated)
            {
                lastline.Text += value;
                if (value.EndsWith("\n"))
                {
                    lastline.Terminated = true;
                }
            }
            else
            {
                var splitter = new BufferSplitterCollection(OutputLineType.Output, value, RawUI, false);
                Output.AddRange(splitter);
            }
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            var splitter = new BufferSplitterCollection(OutputLineType.Output, value, RawUI.BufferSize.Width,
                foregroundColor,
                backgroundColor, false);
            Output.AddRange(splitter);
        }

        public override void WriteLine(string value)
        {
            var splitter = new BufferSplitterCollection(OutputLineType.Output, value, RawUI, true);
            Output.AddRange(splitter);
        }

        public override void WriteErrorLine(string value)
        {
            var splitter = new BufferSplitterCollection(OutputLineType.Error, value, RawUI.BufferSize.Width,
                ConsoleColor.Red,
                ConsoleColor.Black, true);
            Output.HasErrors = true;
            Output.AddRange(splitter);
        }

        public override void WriteDebugLine(string message)
        {
            var splitter = new BufferSplitterCollection(OutputLineType.Debug, "DEBUG: " + message,
                RawUI.WindowSize.Width,
                ConsoleColor.Cyan, RawUI.BackgroundColor, true);
            Output.AddRange(splitter);
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
            var message = Message.Parse(this, "ise:updateprogress");
            message.Arguments.Add("Activity", record.Activity);
            message.Arguments.Add("ActivityId", record.ActivityId.ToString(CultureInfo.InvariantCulture));
            message.Arguments.Add("CurrentOperation", record.CurrentOperation);
            message.Arguments.Add("StatusDescription", record.StatusDescription);
            message.Arguments.Add("ParentActivityId", record.ParentActivityId.ToString(CultureInfo.InvariantCulture));
            message.Arguments.Add("PercentComplete", record.PercentComplete.ToString(CultureInfo.InvariantCulture));
            message.Arguments.Add("RecordType", record.RecordType.ToString());
            message.Arguments.Add("SecondsRemaining", record.SecondsRemaining.ToString(CultureInfo.InvariantCulture));
            var sheerMessage = new SendMessageMessage(message, false);
            if (JobContext.IsJob)
            {
                message.Arguments.Add("JobId", JobContext.Job.Name);
                JobContext.MessageQueue.PutMessage(sheerMessage);
            }
            else
            {
                sheerMessage.Execute();
            }
        }

        public override void WriteVerboseLine(string message)
        {
            var splitter = new BufferSplitterCollection(OutputLineType.Verbose, "VERBOSE: " + message, RawUI.WindowSize.Width,
                ConsoleColor.Yellow, ConsoleColor.Black, true);
            Output.AddRange(splitter);
        }

        public override void WriteWarningLine(string message)
        {
            var splitter = new BufferSplitterCollection(OutputLineType.Warning, "WARNING: " + message, RawUI.BufferSize.Width,
                ConsoleColor.Yellow, ConsoleColor.Black, true);
            Output.AddRange(splitter);
        }

        public override Dictionary<string, PSObject> Prompt(string caption, string message,
            Collection<FieldDescription> descriptions)
        {
            if (JobContext.IsJob)
            {
                object[] options = new object[descriptions.Count];
                for (var i=0 ; i < descriptions.Count; i++)
                {
                    var description = descriptions[i];
                    options[i] = new Hashtable()
                    {
                        ["Title"] = description.Name,
                        ["Name"] = $"var{i}",
                        ["Value"] = description.DefaultValue?.ToString()??string.Empty
                    };
                    
                }
                JobContext.MessageQueue.PutMessage(new ShowMultiValuePromptMessage(options, "600", "800", caption, message, string.Empty, string.Empty, false));
                var values = (object[]) JobContext.MessageQueue.GetResult();

                return values?.Cast<Hashtable>().ToDictionary(value => value["Name"].ToString(), value => PSObject.AsPSObject(value["Value"]));
            }
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName,
            string targetName)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName,
            string targetName, PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options)
        {
            throw new NotImplementedException();
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices,
            int defaultChoice)
        {
            if (Context.Job == null)
            {
                throw new NotImplementedException();
            }

            var parameters =
                new Hashtable(choices.ToDictionary(p => "btn_" + choices.IndexOf(p),
                    p => WebUtil.SafeEncode(p.Label.Replace("&", ""))))
                {
                    {"te", message},
                    {"cp", caption},
                    {"dc", defaultChoice.ToString(CultureInfo.InvariantCulture)}
                };
            Context.Site = Factory.GetSite(Context.Job.Options.SiteName);
            var lineWidth = choices.Count*80 + 140;
            var strLineWidth = lineWidth/8;
            var lineHeight = 0;
            foreach (var line in message.Split('\n'))
            {
                lineHeight += 1 + line.Length/strLineWidth;
            }
            lineHeight = Math.Max(lineHeight*21 + 130,150);
            var dialogResult = JobContext.ShowModalDialog(parameters, "ConfirmChoice",
                lineWidth.ToString(CultureInfo.InvariantCulture), lineHeight.ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrEmpty(dialogResult))
            {
                return int.Parse(dialogResult.Substring(4));
            }
            return -1;
        }

        public Collection<int> PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, IEnumerable<int> defaultChoices)
        {
            Collection<int> results = new Collection<int>();
            var choice = -1;
            do
            {
                choice = PromptForChoice(caption, message, choices, defaultChoices.FirstOrDefault());
                if (choice != -1)
                {
                    results.Add(choice);
                }
            } while (choice != -1);
            return results;
        }
    }
}