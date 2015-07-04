using AHPlugins;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HDTAHPluginHAPlugin
{
    public class HDTAHPluginHAPlugin : AHPlugin
    {
        private Hashtable heartharenaheromapping = new Hashtable();
        private Hashtable heartharenacardidmapping = new Hashtable();

        private int heroid;
        private List<int> pickedcards = new List<int>();

        public override string Name
        {
            get { return "HDTAHPluginHAPlugin"; }
        }

        public override string Author
        {
            get { return "corlettb"; }
        }

        public override Version Version
        {
            get { return new Version("0.0.3"); }
        }

        public HDTAHPluginHAPlugin()
        {
            // Plugin constructor
            Logger.WriteLine("HDTAHPluginHAPlugin constructor");

            // Populate heartharena class mappings
            heartharenaheromapping["druid"] = 1;
            heartharenaheromapping["hunter"] = 2;
            heartharenaheromapping["mage"] = 3;
            heartharenaheromapping["paladin"] = 4;
            heartharenaheromapping["priest"] = 5;
            heartharenaheromapping["rogue"] = 6;
            heartharenaheromapping["shaman"] = 7;
            heartharenaheromapping["warlock"] = 8;
            heartharenaheromapping["warrior"] = 9;

            LoadHearthArenaCards();
            Logger.WriteLine("HearthArena cards loaded");

        }

        private void LoadHearthArenaCards()
        {
            string assemblylocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string heartharenafile = Path.Combine(assemblylocation, "heartharena.json");
            JObject json = JObject.Parse(File.ReadAllText(heartharenafile));

            foreach (var item in json)
            {
                JToken value = item.Value;
                heartharenacardidmapping[value["name"].ToString().ToLower()] = value["id"];
            }
        }

        private string convertList(List<int> list)
        {
            string returnstr = "";
            string seperator = "";
            foreach (int id in list)
            {
                returnstr += seperator + id.ToString();
                seperator = "-";
            }
            if (returnstr == "") 
            {
                returnstr = "-";
            }
            return returnstr;
        }

        private int cardToHAID(Card card)
        {
            string cardname = card.Name.ToLower();
            if (!heartharenacardidmapping.ContainsKey(cardname))
            {
                throw new Exception("Unable to find heartharena mapping for card with name " + cardname);
            }
            return Convert.ToInt32(heartharenacardidmapping[cardname].ToString());
        }

        private List<int> cardsToHAIDs(List<Card> cards)
        {
            List<int> returnlist = new List<int>();
            foreach (Card card in cards)
            {
                returnlist.Add(cardToHAID(card));
            }
            return returnlist;
        }

        private string getHearthArenaUrl(List<Card> newcards)
        {
            return "http://draft.heartharena.com/arena/option-multi-score/" + heroid + "/" + convertList(pickedcards) + "/" + convertList(cardsToHAIDs(newcards));
        }

        private async Task<JObject> getHearthAranaData(string url)
        {
            string heartharenadatastr = "";
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                Logger.WriteLine("Making heartharena call to " + url + "...");
                Stopwatch sw = new Stopwatch();
                sw.Start();
				heartharenadatastr = await wc.DownloadStringTaskAsync(url);
                sw.Stop();
                Logger.WriteLine("... Call took " + sw.ElapsedMilliseconds + "ms");
            }

            JObject heartharenadata = JObject.Parse(heartharenadatastr);

            return heartharenadata;
        }

        public override async Task<List<string>> GetCardValues(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<string> defaultvalues)
        {
            List<string> values = new List<string>();

            string url = getHearthArenaUrl(newcards);
      
            JObject heartharenadata;
            
            try
            {
                heartharenadata = await getHearthAranaData(url);
                values.Add(heartharenadata["results"][0]["card"]["score"].ToString() + " (" + defaultvalues[0] + ")");
                values.Add(heartharenadata["results"][1]["card"]["score"].ToString() + " (" + defaultvalues[1] + ")");
                values.Add(heartharenadata["results"][2]["card"]["score"].ToString() + " (" + defaultvalues[2] + ")");

                string heartharenamessage = heartharenadata["tip"]["text"].ToString();
                Regex rgx = new Regex("<[^>]+/?>");
                heartharenamessage = rgx.Replace(heartharenamessage, "*");
                values.Add("HEARTHARENA: "+heartharenamessage+"\n== Default tier list score in brackets");
            }
            catch 
            {
                for (int i = 0; i < 3; i++)
                {
                    values.Add(defaultvalues[i]);
                }
                values.Add("Failed to get heartharena data. Returning default tier list");
            }
            
            return values;
        }

        public override async void NewArena(ArenaHelper.Plugin.ArenaData arenadata)
        {
            Logger.WriteLine("New Arena: " + arenadata.deckname);
            pickedcards.Clear();
        }

		public override async void HeroesDetected(ArenaHelper.Plugin.ArenaData arenadata, string heroname0, string heroname1, string heroname2)
        {
            Logger.WriteLine("Heroes Detected: " + heroname0 + ", " + heroname1 + ", " + heroname2);
        }

        public override async void HeroPicked(ArenaHelper.Plugin.ArenaData arenadata, string heroname)
        {
            heroid = Convert.ToInt32(heartharenaheromapping[heroname.ToLower()]);
            Logger.WriteLine("Hero Picked: " + heroname + ", id: " + heroid);
        }

        public override async void CardPicked(ArenaHelper.Plugin.ArenaData arenadata, int pickindex, Card card)
        {
            Logger.WriteLine("Card Picked: " + card.Name);
            string cardname = card.Name.ToLower();
            if (!heartharenacardidmapping.ContainsKey(cardname))
            {
                throw new Exception("Unable to find heartharena mapping for card with name " + cardname);
            }
            pickedcards.Add(Convert.ToInt32(heartharenacardidmapping[cardname].ToString()));
        }

        public override async void Done(ArenaHelper.Plugin.ArenaData arenadata)
        {
            Logger.WriteLine("Done");
        }

        public override async void ResumeArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            Logger.WriteLine("Resuming Arena");

            foreach (var heroname in arenadata.detectedheroes)
            {
                ArenaHelper.Plugin.HeroHashData hero = ArenaHelper.Plugin.GetHero(heroname);
                Logger.WriteLine("Detected hero: " + hero.name);
            }

            if (arenadata.pickedhero != "")
            {
                ArenaHelper.Plugin.HeroHashData hero = ArenaHelper.Plugin.GetHero(arenadata.pickedhero);

                heroid = Convert.ToInt32(heartharenaheromapping[hero.name.ToLower()]);
                Logger.WriteLine("Picked hero: " + hero.name + ", id: " + heroid);
            }

            pickedcards.Clear();
            foreach (var cardid in arenadata.pickedcards)
            {
                Card card = ArenaHelper.Plugin.GetCard(cardid);

                pickedcards.Add(cardToHAID(card));
                Logger.WriteLine(card.Name);
            }

            Logger.WriteLine("HearthArena picklist: " + convertList(pickedcards));

            Logger.WriteLine("State: " + ArenaHelper.Plugin.GetState().ToString());
        }

        public override async void CloseArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            Logger.WriteLine("Closing");
        }
    }
}