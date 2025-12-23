using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Logs;
using Newtonsoft.Json;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
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


    public class Questing : Mod
    {

        public void load()
        {

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

        public override void PostSetupContent()
        {
            if (!Main.dedServ)
            {
                MyInterface = new UserInterface();
                QuestingUI = new QuestingUIState();
                QuestingUI.Activate(); // Activate calls Initialize() on the UIState if not initialized and calls OnActivate, then calls Activate on every child element.
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


    }
    public class QuestingUIState : UIState
    {
        Vector2 PanelPosition = new Vector2(200, 100);
        Vector2 PanelSize = new Vector2(800, 800);
        public UIPanel QuestPanel;
        QuestPage[] QuestPages;

        QuestUI[] CurrentPage;
        int PageNumber;
        bool SetupComplete = false;
        public override void OnInitialize()
        {

            base.OnInitialize();
            UseImmediateMode = true;

        }
        public void SetupQuestUI()
        {
            if (SetupComplete)
            {
                return;
            }
            SetupComplete = true;
            // debugQuest();
            string json = LoadJson();
            ReadJson(json);
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
            Main.NewText($"Converting Quests To UI");
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

            foreach (string key in Pages.Keys)
            {
                Main.NewText($"Parsing Page: {key}.");
                QuestPage NewPage = new QuestPage();
                QuestPages[PageCount] = NewPage;

                var Quests = JsonConvert.DeserializeObject<Dictionary<string, Object>>(Pages[key].ToString());

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
                }
            }
            Main.NewText($"Completed Parsing");
        }

        void ExampleQuests()
        {

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
        string[] Dependencies;
        Quest[] Quests;
        int[] ItemIdRewards;
        int[] ItemCountRewards;
        Vector2 Position;
        public bool Completed;

        public QuestNode(Dictionary<string,Object> QuestDict,string NewQuestName)
        {
            QuestName = NewQuestName;
            var SpriteId = Questing.ToID(QuestDict["Sprite"].ToString());
            Main.NewText($"New SpriteID is: {SpriteId}");
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
    }

    public class Quest
    {
        public string QuestType = "None";
        bool Completed;

        public void LoadQuest()
        {

        }
    }

    public class CollectQuest : Quest
    {
        int ItemId;
        int ItemCount;
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
            }
            else
            {
                QuestPanel.BackgroundColor = Color.Gray;
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


}




    