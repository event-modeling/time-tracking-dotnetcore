using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using static System.DateTime;

namespace ConsoleTimeTracking {
    internal class Program {
        private static void Main(string[] args) {
            Console.WriteLine("Hello World!");
            Directory.CreateDirectory("events");
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();
            host.Run();
        }
    }

    public class Startup {
        public void Configure(IApplicationBuilder app) {
            app.Run(context => {
                var requestPath = context.Request.Path;
                switch (requestPath) {
                    case "/log-time.html":
                        if (context.Request.Method == "POST") {
                            var date = ParseExact(context.Request.Form["date"], "yyyy-MM-dd",
                                CultureInfo.InvariantCulture);
                            var loggedTimeEvent = new LoggedTimeEvent(
                                date,
                                context.Request.Form["hour"],
                                context.Request.Form["minute"],
                                context.Request.Form["total-hours"],
                                context.Request.Form["project"]);
                            File.WriteAllText("events/" + Now.ToUniversalTime() + "-LoggedTime.json", JsonSerializer.Serialize(loggedTimeEvent));
                        }
                        return context.Response.WriteAsync(FillTemplate("layout/log-time.html",
                            GetTodayAndYesterday()));
                    default:
                        return context.Response.WriteAsync("Unknown path: " + requestPath);
                }
            });
        }

        private Model GetTodayAndYesterday() {
            return Directory.EnumerateFiles("events")
                .Select(file => {
                    var readAllText = File.ReadAllText(file);
                    return JsonSerializer.Deserialize<LoggedTimeEvent>(readAllText);
                })
                .Where(logEntry => logEntry.Day > Today.Date.AddDays(-2))
                .Aggregate(new TodayAndYesterdayModel(), (model, loggedTimeEvent) => model.Add(loggedTimeEvent));
        }

        private string FillTemplate(string template, Model model) {
            var tokenTree = new TokenTree(Tokenize(template), "");
            var resultString = tokenTree.PopulateWith(model);
            return resultString;
        }

        private static IEnumerator<string> Tokenize(string template) {
            var templateContents = File.ReadAllText(template);
            var tokens = Regex.Split(templateContents, @"({{[^{}]*}})");
            return tokens.Cast<string>().GetEnumerator();
        }
    }

    internal class TodayAndYesterdayModel : Model {

        public TodayAndYesterdayModel Add(LoggedTimeEvent loggedTimeEvent) {
            var day = loggedTimeEvent.Day == Now.AddDays(-1).Date ? "yesterday" : "today";
            if (!Children.ContainsKey(day)) {
                Children.Add(day, new List<Model>());
            }
            var loggedTimeModel = new Model();
            loggedTimeModel.Data["StartHour"] = loggedTimeEvent.StartHour;
            loggedTimeModel.Data["StartMinute"] = loggedTimeEvent.StartMinute;
            loggedTimeModel.Data["Project"] = loggedTimeEvent.Project;
            loggedTimeModel.Data["Hours"] = loggedTimeEvent.TotalHours;
            Children[day].Add(loggedTimeModel);
            return this;
        }
    }

    internal class Model {
        protected readonly Dictionary<string, List<Model>> Children = new Dictionary<string, List<Model>>();
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public bool HasChildrenFor(string rangeName) { return Children.ContainsKey(rangeName); }
        public List<Model> this[string key] => Children[key];
    }

    internal class TokenTree : TokenItem {
        private readonly List<TokenItem> _tokenItems = new List<TokenItem>();
        private string Empty { get; }

        public TokenTree(IEnumerator<string> tokenEnumerator, string rangeName) : base(rangeName) {
            var accumulateEmpty = false;
            Empty = "";
            while (tokenEnumerator.MoveNext())
                if      (RangeIdentified()) _tokenItems.Add(new TokenTree(tokenEnumerator, tokenEnumerator.Current));
                else if (EndOfRangeIdentified()) return;
                else if (EmptinessIdentified()) accumulateEmpty = true;
                else if (accumulateEmpty) Empty += tokenEnumerator.Current; 
                else                      _tokenItems.Add(new TokenItem(tokenEnumerator.Current));

            bool EndOfRangeIdentified() {      return Regex.Matches(tokenEnumerator.Current!, @"{{ end }}").Count == 1; }
            bool EmptinessIdentified() {       return Regex.Matches(tokenEnumerator.Current!, @"{{ if-nothing }}").Count == 1; }
            bool RangeIdentified() {           return Regex.Matches(tokenEnumerator.Current!, @"{{ range .* }}").Count == 1; }
        }

        public string PopulateWith(Model model) {
            var resultString = new StringBuilder();
            foreach (var tokenItem in _tokenItems) 
                if (tokenItem is TokenTree) 
                    if (OneOrMoreModelInstancesIdentified(tokenItem)) GetModelInstances(tokenItem).ForEach(
                        thisModel => PopulateNestedTemplate(tokenItem, thisModel));
                    else resultString.Append(GetEmptyMessageFor(tokenItem));
                else if (IsPlaceHolder(tokenItem)) resultString.Append(GetValueFor(tokenItem)); 
                else resultString.Append(tokenItem.Token);
            return resultString.ToString();

            bool IsPlaceHolder(TokenItem tokenItem) { return Regex.Match(tokenItem.Token, @"{{.*}}").Success; }
            string FieldName(TokenItem tokenItem) { return Regex.Replace(tokenItem.Token, @"{{ (.*) }}", @"$1"); }
            string RangeName(TokenItem tokenItem) { return Regex.Replace(tokenItem.Token, @"{{ range (.*) }}", @"$1");}
            bool OneOrMoreModelInstancesIdentified(TokenItem tokenItem) { return model.HasChildrenFor(RangeName(tokenItem)) && model[RangeName(tokenItem)].Count != 0; }
            List<Model> GetModelInstances(TokenItem tokenItem) { return model[RangeName(tokenItem)]; }
            void PopulateNestedTemplate(TokenItem tokenItem, Model thisModel) { resultString.Append(((TokenTree) tokenItem).PopulateWith(thisModel)); }
            string GetValueFor(TokenItem tokenItem) { return model.Data[FieldName(tokenItem)]; }
            string GetEmptyMessageFor(TokenItem tokenItem) { return ((TokenTree) tokenItem).Empty; }
        }
    }

    internal class TokenItem {
        public readonly string Token;
        public TokenItem(string token) { Token = token; }
    }

    public class LoggedTimeEvent {
        public LoggedTimeEvent() {}
        public LoggedTimeEvent(in DateTime day, in string startHour, in string startMinute, in string totalHours,
            in string project) {
            Day = day;
            StartHour = startHour;
            StartMinute = startMinute;
            TotalHours = totalHours;
            Project = project;
        }

        public string StartHour { get; set; }
        public string StartMinute { get; set; }
        public string TotalHours { get; set; }
        public string Project { get; set; }
        public DateTime Day { get; set; }
    }
};