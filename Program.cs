// 

namespace Buffs2Csv
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using CsvHelper;

    using OpenQA.Selenium;
    using OpenQA.Selenium.Chrome;

    public struct Player
    {
        public string Name { get; }

        public int WCB { get; }

        public int Ony { get; }

        public int ZG { get; }

        public int SF { get; }

        public int DMF { get; }

        public int UBRS { get; }

        public int DMT1 { get; }

        public int DMT2 { get; }

        public int DMT3 { get; }

        public List<WorldBuff> WorldBuffs { get; }

        public Player(string name, List<WorldBuff> buffs)
        {
            this.Name = name;
            this.WorldBuffs = buffs;

            this.WCB = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.WCB) ? 1 : 0;
            this.Ony = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.Ony) ? 1 : 0;
            this.ZG = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.SpiritOfZandalar) ? 1 : 0;
            this.SF = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.Songflower) ? 1 : 0;
            this.DMF = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.DMF) ? 1 : 0;
            this.DMT1 = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.FengusFerocity) ? 1 : 0;
            this.DMT2 = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.SlipkiksSavvy) ? 1 : 0;
            this.DMT3 = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.MoldarsMoxie) ? 1 : 0;
            this.UBRS = this.WorldBuffs.Any(b => b.Kind == WorldBuffType.UBRSFireResist) ? 1 : 0;
        }

        public void AddBuff(WorldBuff b)
        {
            this.WorldBuffs.Add(b);
        }

        /// <inheritdoc />
    }

    public struct WorldBuff
    {
        public WorldBuffType Kind { get; }

        public WorldBuff(uint id)
        {
            this.Kind = (WorldBuffType)id;
        }
    }

    public enum WorldBuffType
    {
        SpiritOfZandalar = 24425,

        FengusFerocity = 22817,

        MoldarsMoxie = 22818,

        SlipkiksSavvy = 22820,

        UBRSFireResist = 15123,

        DMF = 23768,

        Ony = 22888,

        WCB = 16609,

        Songflower = 15366
    }

    static class Program
    {
        private static string raidId;

        private static ChromeDriver D { get; set; }

        private static List<Player> Raiders { get; set; }

        private static Exception AppException { get; set; }

        private static bool Done { get; set; }

        private static string LogName { get; set; }

        private static void Main(string[] args)
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "logs")) Directory.CreateDirectory("logs");

            while (true)
            {
                raidId = string.Empty;
                D = null;
                Raiders = new List<Player>();
                AppException = null;
                Done = false;
                LogName = string.Empty;

                do
                {
                    Console.WriteLine("Copy/paste inn logg-ID fra URL (f.eks G3xpg6CWXdHrvaDJ). Trykk Enter");
                    Console.WriteLine("NB! Krever at du har Chrome 83.0 installert");
                    Console.WriteLine();

                    raidId = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(raidId))
                    {
                        Console.WriteLine("Log ID ble ikke oppgitt");
                        continue;
                    }

                    if (raidId?.Length != 16) Console.WriteLine("Logg-ID ble oppgitt i feil format, skal være 16 tegn");
                }
                while (string.IsNullOrEmpty(raidId) || raidId.Length != 16);

                Raiders = new List<Player>();

                ChromeOptions opt = new ChromeOptions();

                opt.AddArgument(
                    "--user-agent=Mozilla/5.0 (iPad; CPU OS 6_0 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/6.0 Mobile/10A5355d Safari/8536.25");
                opt.AddArgument("--log-level=3");
                opt.AddArgument("--disable-logging");
                opt.AddArgument("--headless");

                ChromeDriverService s = ChromeDriverService.CreateDefaultService();

                s.SuppressInitialDiagnosticInformation = true;

                try
                {
                    D = new ChromeDriver(s, opt);
                }
                catch (Exception e)
                {
                    AppException = e;
                    continue;
                }

                Task.Factory.StartNew(Manager);

                while (!Done)
                {
                }

                D.Close();

                if (AppException == null)
                {
                    Console.Clear();
                    Console.WriteLine(
                        "Wbuffs lagret til: " + AppDomain.CurrentDomain.BaseDirectory + @"\logs\" + LogName + ".csv");
                }
                else
                {
                    Console.WriteLine(AppException.Message);
                }

                Console.WriteLine();
            }
        }

        private static uint GetSpellIdFromHref(string href)
        {
            var parts = href.Split('=');

            return uint.Parse(parts[1]);
        }

        private static string GetPlayerNameFromRowText(string text)
        {
            var parts = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            return parts[0];
        }

        private static async Task Manager()
        {
            D.Url = "https://classic.warcraftlogs.com/reports/" + raidId + "#boss=-2&difficulty=0&wipes=2";

            var source = D.PageSource;

            LogName = D.FindElementByCssSelector("#report-title-container").Text.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None)[0].Replace(" ", "_");

            var tableRows = await GetRows().ConfigureAwait(false);

            IWebElement[] all = new IWebElement[tableRows.Count];

            tableRows.CopyTo(all, 0);

            Task[] tasks = new Task[all.Length];

            Raiders = new List<Player>();

            int i = 0;

            foreach (var e in all)
            {
                try
                {
                    tasks[i] = Task.Factory.StartNew(() => BuildPlayer(e));
                }
                catch (Exception exception)
                {
                    AppException = exception;
                }

                i++;
            }

            Task.WaitAll(tasks);

            Raiders.OrderBy(r => r.Name);

            try
            {
                using (var writer = new StreamWriter(@"logs\" + LogName + ".csv", false, Encoding.UTF8))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(Raiders);
                }
            }
            catch (Exception e)
            {
                AppException = e;
            }

            Done = true;
        }

        private static Player BuildPlayer(IWebElement e)
        {
            string playerName = GetPlayerNameFromRowText(e.Text);

            Console.WriteLine("Henter wbuffs for " + playerName);

            List<WorldBuff> buffs = new List<WorldBuff>();

            foreach (var b in e.FindElements(By.CssSelector("td.azerite-cell > a:nth-child(n)"))
                .Select(x => x.GetAttribute("href")))
            {
                try
                {
                    uint id = GetSpellIdFromHref(b);
                    WorldBuff buff = new WorldBuff(id);
                    buffs.Add(buff);
                }
                catch (Exception exception)
                {
                    AppException = exception;
                }
            }

            Player p = new Player(playerName, buffs);

            lock (Raiders)
            {
                if (Raiders.All(r => r.Name != p.Name)) Raiders.Add(p);
            }

            return p;
        }

        private static async Task<ReadOnlyCollection<IWebElement>> GetRows()
        {
            Console.WriteLine("Henter Rader");

            List<IWebElement> res = new List<IWebElement>();

            while (res.Count == 0)
            {
                await Task.Delay(100).ConfigureAwait(false);

                var t1 = D.FindElementsByCssSelector("#DataTables_Table_0 > tbody > tr:nth-child(n)").ToList();
                var t2 = D.FindElementsByCssSelector("#DataTables_Table_1 > tbody > tr:nth-child(n)").ToList();
                var t3 = D.FindElementsByCssSelector("#DataTables_Table_2 > tbody > tr:nth-child(n)").ToList();

                if (t1.Count == 0 || t2.Count == 0 || t3.Count == 0) continue;

                res.AddRange(t1);
                res.AddRange(t2);
                res.AddRange(t3);
            }

            Console.WriteLine("Fant " + res.Count + " rader");

            return new ReadOnlyCollection<IWebElement>(res);
        }
    }
}