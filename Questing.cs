using Humanizer;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod.Logs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.UI;

namespace Questing
{
    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class QuestCommand : ModCommand
    {
        public static LocalizedText DescriptionText { get; private set; }

        public override void SetStaticDefaults()
        {
            DescriptionText = Mod.GetLocalization($"Commands.{nameof(QuestCommand)}.Description");
        }

        // CommandType.Chat means that command can be used in Chat in SP and MP
        public override CommandType Type
            => CommandType.Chat;

        // The desired text to trigger this command
        public override string Command
            => "quest";

        // A short description of this command
        public override string Description
            => DescriptionText.Value;

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            ModContent.GetInstance<QuestingSystem>().AlternateUI();
        }
    }

    public class QuestGlobalItem : GlobalItem
    {
        public override bool OnPickup(Item item, Player player)
        {
            //ModContent.GetInstance<QuestingSystem>().PlayerInventoryChanged(player);
            return true;
        }
    }

    public class QuestPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Questing.OpenQuestMenu.JustPressed) // Use JustPressed for a fire-once action
            {
                // Put the code you want to run when the key is pressed here
                ModContent.GetInstance<QuestingSystem>().AlternateUI();
                // Example: Teleport the player
                // Player.Teleport(Main.MouseWorld);
            }

            if (Questing.OpenQuestMenu.Current) // Use Current if the action should happen while the key is held down
            {
                // Code for continuous action
            }
        }
    }
    public class Questing : Mod
    {

        public static ModKeybind OpenQuestMenu;

        public override void Load()
        {
            // Register a new keybind with a unique internal name and a default key (e.g., 'P')
            OpenQuestMenu = KeybindLoader.RegisterKeybind(this, "ToggleQuestMenu", "Y");
        }

        public override void Unload()
        {
            // Unload the keybind to prevent memory leaks when the mod is disabled
            OpenQuestMenu = null;
        }

        public static int ToID(string Id_)
        {
            int ItemId;
            var isNumeric = int.TryParse(Id_, out int NewId);
            if (isNumeric)
            {
                ItemId = NewId;
                Main.NewText($"Converting Integer Id: {Id_} to: {ItemId}");

            }
            else
            {
                ItemId = ItemID.Search.GetId(Id_);
                Main.NewText($"Converting String Id: {Id_} to: {ItemId}");

            }
            return ItemId;
        }
    }
    public class QuestingSystem : ModSystem
    {
        internal UserInterface MyInterface;
        internal QuestingUIState QuestingUI;

        public override void Load()
        {
            
        }
        public override void PostSetupContent()
        {
            if (!Main.dedServ)
            {
                MyInterface = new UserInterface();
                QuestingUI = new QuestingUIState();
                QuestingUI.Activate(); // Activate calls Initialize() on the UIState if not initialized and calls OnActivate, then calls Activate on every child element.
                QuestingUI.SetupQuestUI();
            }

        }

        private GameTime _lastUpdateUiGameTime;

        public override void UpdateUI(GameTime gameTime)
        {
            _lastUpdateUiGameTime = gameTime;
            if (MyInterface?.CurrentState != null)
            {
                MyInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "MyMod: MyInterface",
                    delegate
                    {
                        if (_lastUpdateUiGameTime != null && MyInterface?.CurrentState != null)
                        {
                            MyInterface.Draw(Main.spriteBatch, _lastUpdateUiGameTime);
                        }
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
        internal void AlternateUI()
        {
            if (MyInterface?.CurrentState != null)
            {
                HideMyUI();
            }
            else
            {
                ShowMyUI();

            }
        }
        internal void ShowMyUI()
        {
            MyInterface?.SetState(QuestingUI);
            QuestingUI.SetupQuestUI();
        }

        internal void HideMyUI()
        {
            MyInterface?.SetState(null);
        }

        public override void PostUpdatePlayers()
        {
            QuestUpdate(Main.LocalPlayer);
        }
        public void QuestUpdate(Player player)
        {
            QuestingUI.QuestUpdate(player);
        }
    }
    public class QuestingUIState : UIState
    {
        Vector2 PanelPosition = new Vector2(200, 100);
        Vector2 PanelSize = new Vector2(800, 800);
        public UIPanel QuestPanel;
        QuestPage[] QuestPages;
        List<DependencyLinesUI> DepLines;
        QuestUI[] CurrentPage;
        int PageNumber;
        bool SetupComplete = false;
        bool QuestsLoaded = false;

        public override void OnInitialize()
        {

            base.OnInitialize();
            UseImmediateMode = true;

        }
        public void LoadQuests()
        {
            string json = LoadJson();
            ReadJson(json);
        }
        public void SetupQuestUI()
        {
            //Main.NewText("Setting Up UI");
            if (!QuestsLoaded)
            {
                LoadQuests();
                QuestsLoaded = true;

            }
            if (SetupComplete)
            {
                return;
            }
            SetupComplete = true;
            // debugQuest();

            LoadPage(0);
            QuestPanel = new UIPanel();
            QuestPanel.Left.Set(PanelPosition.X, 0f);
            QuestPanel.Top.Set(PanelPosition.Y, 0f);
            QuestPanel.Width.Set(PanelSize.X, 0f);
            QuestPanel.Height.Set(PanelSize.Y, 0f);
            Append(QuestPanel);

            foreach (QuestUI Q in CurrentPage)
            {
                QuestPanel.Append(Q);
                QuestPanel.Append(Q.InitPanel());
                QuestPanel.Append(Q.InitImage());
            }

        }
        public QuestUI[] GetQuests(int CurrentPage, bool ShowUnavailableQuests = true)
        {
            //Main.NewText($"Converting Quests To UI");
            QuestUI[] QUI = new QuestUI[QuestPages[CurrentPage].AllQuests.Count()];
            int QuestCount = 0;
            foreach (QuestNode Q in QuestPages[CurrentPage].AllQuests)
            {
                QUI[QuestCount] = (Q.getQuestUI());
                QuestCount++;
            }
            return QUI;
        }

        public string LoadJson()
        {
            Main.NewText($"Loading Json from file");
            string json = "";
            //Stream stream = Questing.GetStream("Content/Quest.json");
            string filePath = "C:/Users/jack/OneDrive/Documents/My Games/Terraria/tModLoader/ModSources/Questing/Content/Quest.json";
            using (StreamReader r = new StreamReader(filePath))
            {
                json = r.ReadToEnd();
            }
            return json;
        }

        public void ReadJson(string json)
        {
            int PageCount = 0;
            int QuestCount = 0;

            Main.NewText($"Parsing Json.");

            var Pages = JsonConvert.DeserializeObject<Dictionary<string, Object>>(json);
            QuestPages = new QuestPage[Pages.Keys.Count];
            List<String> LinesFrom = new List<string>();
            List<String> LinesTo = new List<string>();
            DepLines = new List<DependencyLinesUI>();


            foreach (string key in Pages.Keys)
            {
                Main.NewText($"Parsing Page: {key}.");
                QuestPage NewPage = new QuestPage();
                QuestPages[PageCount] = NewPage;

                var PageInfo = JsonConvert.DeserializeObject<Dictionary<string, Object>>(Pages[key].ToString());
                var Quests = JsonConvert.DeserializeObject<Dictionary<string, Object>>(PageInfo["Quests"].ToString());

                NewPage.Load(Quests.Keys.Count, key);
                PageCount += 1;
                QuestCount = 0;

                foreach (string Quest  in Quests.Keys)
                {
                    Main.NewText($"Parsing Quest: {Quest}.");

                    var ThisQuest = JsonConvert.DeserializeObject<Dictionary<string, Object>>(Quests[Quest].ToString());
                    QuestNode NewQuest = new QuestNode(ThisQuest,Quest);
                    NewPage.AllQuests[QuestCount] = NewQuest;
                    QuestCount += 1;
                    foreach (string Dep in NewQuest.Dependencies)
                    {
                        LinesFrom.Add(Quest);
                        LinesTo.Add(Dep);
                    }
                }
            }
            Main.NewText($"Completed Parsing");
            for (int i = 0; i < LinesFrom.Count();  i++ )
            {
                DependencyLinesUI NewLine = new DependencyLinesUI(GetQuest(LinesFrom[i]).Position, GetQuest(LinesTo[i]).Position);
                DepLines.Add(NewLine);
            }
        }

        public void QuestUpdate(Player player)
        {
            if (!SetupComplete)
            {
                return;
            }
            bool resetUI = false;
            QuestNode[] UnlockedQuests = GetUnlockedQuests();
            if (UnlockedQuests == null) {
                //Main.NewText("No Unlocked Quests");
                return; 
            }
            foreach (QuestNode Node in UnlockedQuests)
            {
                bool QuestComplete = true;
                foreach (CollectQuest Collect in Node.Quests)
                {
                    if (Collect.QuestType == "Collect")
                    {
                        if (player.CountItem(Collect.ItemId) >= Collect.ItemCount){
                            Collect.Completed = true;
                            QuestComplete = true;
                        }
                    }
                    if (!Collect.Completed)
                    {
                        QuestComplete = false;
                    }
                }
                if (!Node.Completed && QuestComplete)
                {
                    resetUI = true;
                    Main.NewText($"Quest Complete: {Node.QuestName}");
                    Node.SetCompleted();

                }
            }
            if (resetUI)
            {
                ResetUI();
                SetupQuestUI();
            }
        }

        public QuestNode[] GetUnlockedQuests()
        {
            //Main.NewText("Making New list");
            List<QuestNode> UnlockedQuests = new List<QuestNode>();
            //Main.NewText("Making New list");
            foreach (QuestPage Page in QuestPages)
            {

                foreach (QuestNode Node in Page.AllQuests)
                {

                    if (IsQuestUnlocked(Node))
                    {
                        UnlockedQuests.Add(Node);

                    }
                }
            }
            //Main.NewText($"Return New List of size: {UnlockedQuests.Count}");
            return UnlockedQuests.ToArray();
        }

        public bool IsQuestUnlocked(QuestNode Node)
        {
            //Main.NewText(Node.DependencyType);
            if (Node.DependencyType == "Required")
            {
                bool Unlocked = true;
                foreach (string DependantQ in Node.Dependencies)
                {
                    QuestNode DQ = GetQuest(DependantQ);
                    if (DQ != null && !DQ.Completed)
                    {
                        //Main.NewText($"Dependancy Not Complete: {Node.QuestName}");
                        Unlocked = false;
                    }
                }
                return Unlocked;
            }
            else if(Node.DependencyType == "Atleast One")
            {
                bool Unlocked = false;
                foreach (string DependantQ in Node.Dependencies)
                {
                    QuestNode DQ = GetQuest(DependantQ);
                    if (DQ != null && DQ.Completed)
                    {
                        //Main.NewText($"Dependancy Not Complete: {Node.QuestName}");
                        Unlocked = true;
                    }
                }
                return Unlocked;
            }
            else
            {
                return true;
            }
        }
        public QuestNode GetQuest(string QuestName) {
            foreach (QuestPage Page in QuestPages)
            {
                foreach (QuestNode Node in Page.AllQuests)
                {
                    if (Node.QuestName == QuestName)
                    {
                        return Node;
                    }
                }
            }
            return null;
        }

        public void ResetUI()
        {
            SetupComplete = false;
            RemoveAllChildren();
        }
        public void LoadPage(int PageNum)
        {
            PageNumber = PageNum;
            CurrentPage = GetQuests(PageNumber, true);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {

            // Drawing first line of coins (current collected coins)
            // CoinsSplit converts the number of copper coins into an array of all types of coins

            base.DrawSelf(spriteBatch);

            //Main.NewText($"Drawing Quests.");
            //spriteBatch.
            //spriteBatch.Begin(SpriteSortMode.Immediate,BlendState.Opaque);
            //DrawQuests(spriteBatch, PanelPosition);
        }

        void DrawQuests(SpriteBatch spriteBatch, Vector2 PanelPosition)
        {
            foreach (QuestUI Q in CurrentPage)
            {
                //spriteBatch.Draw(Q.Sprite, Q.Position + PanelPosition + new Vector2(50, 50), null, Color.White, 0f, Q.Sprite.Size() / 2, 1f, SpriteEffects.None, 0f);
                //Q.DrawQuest(spriteBatch,PanelPosition);
                //spriteBatch.Draw(Q.Sprite, new Vector2(shopx + Q.Position.X, shopy + Q.Position.Y), null, Color.White, 0f, Q.Sprite.Size() / 2f, 1f, SpriteEffects.None, 0f);
            }

        }
    }

    public class QuestPage
    {
        public QuestNode[] AllQuests;
        public String PageName;
        public Texture2D PageSprite;

        public void Load(int Quests,string NewPageName)
        {
            AllQuests = new QuestNode[Quests];
            PageName = NewPageName;

        }
        public bool GetQuestComplete(string QuestName)
        {
            foreach (QuestNode Q in AllQuests)
            {
                if (Q.QuestName == QuestName)
                {
                    return Q.Completed;
                }
            }
            return false;
        }
    }

    public class QuestNode
    {
        Texture2D Sprite;
        public string QuestName;
        string Description;
        public string[] Dependencies;
        public string DependencyType;
        public Quest[] Quests;
        Dictionary<int, int> Rewards;
        public Vector2 Position;
        public bool Completed;

        public QuestNode(Dictionary<string,Object> QuestDict,string NewQuestName)
        {
            QuestName = NewQuestName;
            var SpriteId = Questing.ToID(QuestDict["Sprite"].ToString());
            //Main.NewText($"New SpriteID is: {SpriteId}");
            Main.instance.LoadItem(SpriteId);
            Sprite = TextureAssets.Item[SpriteId].Value;
            var NewPosition = JsonConvert.DeserializeObject<Dictionary<string, float>>(QuestDict["Position"].ToString());
            Position = new Vector2(NewPosition["X"], NewPosition["Y"]);
            var QuestComponents = JsonConvert.DeserializeObject<Dictionary<string, int>>(QuestDict["Quests"].ToString());
            Quests = new Quest[QuestComponents.Keys.Count];
            int QuestCount = 0;
            foreach (string Q in QuestComponents.Keys)
            {
                CollectQuest NewQ = new CollectQuest(Q, QuestComponents[Q]);
                Quests[QuestCount] = NewQ;
                QuestCount++;
            }
            JArray FuckedUp = (JArray)QuestDict["Dependencies"];
            DependencyType = FuckedUp[0].ToString();
            Dependencies = new string[FuckedUp.Count - 1];
            for (int i = 1; i < FuckedUp.Count; i++)
            {
                Dependencies[i - 1] = FuckedUp[i].ToString();
            }
            if (true)
            {
                var TempAwards = JsonConvert.DeserializeObject<Dictionary<string, int>>(QuestDict["Rewards"].ToString());
                Rewards = new();
                foreach (string key in TempAwards.Keys)
                {
                    Rewards.Add(Questing.ToID(key), TempAwards[key]);
                }
            }
        }


        public QuestUI getQuestUI()
        {
            QuestUI questUI = new QuestUI();
            questUI.Sprite = Sprite;
            questUI.QuestName = QuestName;
            questUI.Position = Position;
            questUI.Completed = Completed;
            return questUI;
        }

        public void SetCompleted()
        {
            Completed = true;
        }
    }

    public class Quest
    {
        public string QuestType = "None";
        public bool Completed;

        public void LoadQuest()
        {

        }
    }

    public class CollectQuest : Quest
    {
        public int ItemId;
        public int ItemCount;
        public CollectQuest(string Item, int Count)
        {
            QuestType = "Collect";
            ItemId = Questing.ToID(Item);
            ItemCount = Count;
        }

        public void LoadQuest(string Item, int Count)
        {
            QuestType = "Collect";
            ItemId = Questing.ToID(Item);
            ItemCount = Count;
        }
    }

    public class KillQuest : Quest
    {
        int EnemyId;
        int EnemyCount;
    }

    public class QuestUI : UIElement
    {
        public Texture2D Sprite;
        public string QuestName;
        public Vector2 Position;
        public bool Completed;
        public UIPanel QuestPanel;
        public UIImage QuestImage;

        public QuestUI()
        {

        }
        public UIPanel InitPanel()
        {
            QuestPanel = new UIPanel();
            QuestPanel.Left.Set(Position.X, 0f);
            QuestPanel.Top.Set(Position.Y, 0f);
            QuestPanel.Width.Set(50, 0f);
            QuestPanel.Height.Set(50, 0f);
            if (Completed) {
                QuestPanel.BackgroundColor = Color.Green;
                QuestPanel.BorderColor = Color.LightGreen;
            }
            else
            {
                QuestPanel.BackgroundColor = Color.Gray;
                QuestPanel.BorderColor = Color.DarkRed;
            }
            return QuestPanel;
        }
        public UIImage InitImage()
        {
            QuestImage = new UIImage(Sprite);
            QuestImage.Left.Set(Position.X + 25 - Sprite.Size().X / 2, 0f);
            QuestImage.Top.Set(Position.Y + 25 - Sprite.Size().Y / 2, 0f);
            //Append(QuestPanel);
            return QuestImage;
        }
    }

    public class DependencyLinesUI : UIElement
    {
        public UIImage UILine;

        public DependencyLinesUI(Vector2 FirstNode,Vector2 SecondNode) 
        {
            bool useX = false;
            bool FirstNodeFirst = false;
            Vector2 StartPoint = new Vector2();
            Vector2 EndPoint = new Vector2();
            if (Math.Abs(FirstNode.X - SecondNode.X) > Math.Abs(FirstNode.Y - FirstNode.Y))
            {
                useX = true;
            }
            if (useX)
            {
                if (FirstNode.X - SecondNode.X < 0)
                {
                    FirstNodeFirst = true;
                }
            }
            else
            {
                if (FirstNode.Y - SecondNode.Y < 0)
                {
                    FirstNodeFirst = true;
                }
            }
            if (useX && FirstNodeFirst)
            {
                StartPoint = FirstNode + new Vector2(50, 25);
                EndPoint = SecondNode + new Vector2(0, 25);
            }else if(!useX && FirstNodeFirst) {
                StartPoint = FirstNode + new Vector2(25, 50);
                EndPoint = SecondNode + new Vector2(25, 0);
            }else if(useX && !FirstNodeFirst)
            {
                StartPoint = SecondNode + new Vector2(50, 25);
                EndPoint = FirstNode + new Vector2(0, 25);
            }else if(!useX && !FirstNodeFirst)
            {
                StartPoint = SecondNode + new Vector2(25, 50);
                EndPoint = FirstNode + new Vector2(25, 0);
            }

            float width = (EndPoint - StartPoint).Length();
            float angle = StartPoint.AngleTo(EndPoint);
            Main.instance.LoadItem(3);
            Texture2D newImage = TextureAssets.Item[3].Value;
            UILine = new UIImage(newImage);
            UILine.Color = Color.Black;
            UILine.Left.Set(StartPoint.X, 0f);
            UILine.Top.Set(StartPoint.Y, 0f);
            UILine.Width.Set(width, 0f);
            UILine.Height.Set(2, 0f);
            UILine.Rotation = angle;
        }
        public UIImage GetLine()
        {
            return UILine;
        }
    }
}




    