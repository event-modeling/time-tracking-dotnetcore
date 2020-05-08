using System;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ConsoleTimeTracking {
    class Program {
        static void Main(string[] args) {
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
                switch (requestPath)
                {
                    case "/log-time-form.html":
                        return context.Response.WriteAsync(File.ReadAllText("layout/log-time.html"));
                    
                    case "/log-time":
                        var date = context.Request.Form["date"];
                        var startHour = context.Request.Form["start-hour"];
                        var startMinute = context.Request.Form["start-minute"];
                        var totalHours = context.Request.Form["total-hours"];
                        var project = context.Request.Form["project"];
                        
                        var loggedEvent = new LoggedTimeEvent(date, startHour, startMinute, totalHours, project);
                        var loggedEventJson = JsonSerializer.Serialize<LoggedTimeEvent>(loggedEvent);
                        File.WriteAllText("events/sampleevent.json",loggedEventJson);
                        
                        return context.Response.WriteAsync(File.ReadAllText("layout/confirm-logged-time.html")
                            .Replace("{{ project }}", project)
                            .Replace("{{ date }}", date)
                            .Replace("{{ hour }}", startHour)
                            .Replace("{{ minute }}", startMinute)
                            .Replace("{{ total-time }}", totalHours));
                    default:
                        return context.Response.WriteAsync("Unknown path: " + requestPath);
                }
            });
        }
    }
    public class LoggedTimeEvent {
        public string Date { get; set; }
        public string StartHour { get; set; }
        public string StartMinute { get; set; }
        public string TotalHours { get; set; }
        public string Project { get; set; }

        public LoggedTimeEvent(in string date, in string startHour, in string startMinute, in string totalHours, in string project) {
            Date = date;
            StartHour = startHour;
            StartMinute = startMinute;
            TotalHours = totalHours;
            Project = project;
        }
    }
}