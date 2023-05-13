using XenophyteAndroidWallet.Graphic;
using XenophyteAndroidWallet.Language;
using XenophyteAndroidWallet.User;
using XenophyteAndroidWallet.WalletDatabase;
using XenophyteAndroidWallet.Sync;
using XenophyteAndroidWallet.Event;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XenophyteAndroidWallet.Wallet;
#if DEBUG
using System.Diagnostics;
#endif
namespace XenophyteAndroidWallet
{
    public class ClassListMenu
    {
        public const string MenuAnimation = "animation"; // Animation of the desktop wallet.
        public const string MenuAll = "all"; // Indicate to use a graphic ressource on all menu.
        public const string MenuWelcome = "welcome"; // First initialization of the android wallet menu.
        public const string MenuMain = "main"; // Main menu.
        public const string MenuMainPassword = "mainPassword"; // Main password menu.
        public const string MenuSendTransaction = "sendTransaction";
        public const string MenuTransactionHistory = "transactionHistory";
        public const string MenuExchange = "exchangeMenu";
    }


    public class Interface : Game
    {
        public GraphicsDeviceManager Graphics;

        // Draw object. Draw texture(s), font(s).
        private SpriteBatch _spriteBatch;
        private bool _graphicInit;

        // Current menu name, used by the GraphicEvent object.
        public string CurrentMenu;

        /// <summary>
        /// Entry point.
        /// </summary>
        public Startup StartupObject;


        /// <summary>
        /// About wallet objects.
        /// </summary>
        public ClassWalletDatabase WalletDatabase; // Contain wallet(s).
        public ClassSyncDatabase SyncDatabase; // Contain transaction(s).
        public ClassSortingTransaction SortingTransaction; // Sorting transaction(s).

        /// <summary>
        /// About the network objects.
        /// </summary>
        public ClassSyncNetwork SyncNetwork; // Sync with the network.
        public ClassWalletUpdater WalletUpdater; // Wallet Updater system, retrieve back balance(s) and more.

        /// <summary>
        /// About graphic objects.
        /// </summary>
        public ClassGraphicEvent GraphicEvent; // Graphic event functions (inputs, buttons).
        public GraphicContent GraphicContent; // Graphic content (textures, fonts, pannel, buttons).
        public GraphicManager GraphicManager; // Graphic Manager, draw contents.

        /// <summary>
        /// About language object.
        /// </summary>
        public ClassLanguage LanguageObject; // Contain language (Should be linked to the android system language later).


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="startupObject"></param>
        public Interface(Startup startupObject)
        {
            InitDefaultGraphicSetting();

            StartupObject = startupObject;
#if DEBUG
            Debug.WriteLine("Android Wallet on initialization step.");
#endif
            InitObject();

#if DEBUG
            Debug.WriteLine("Android Wallet Object(s) initialized.");
#endif

            CurrentMenu = ClassListMenu.MenuAnimation;

            /*
            // If any wallet is found.
            if (!WalletDatabase.CheckWalletDatabase())
                CurrentMenu = ClassListMenu.MenuWelcome;
            else
                CurrentMenu = ClassListMenu.MenuMainPassword;
            
            */
#if DEBUG
            Debug.WriteLine("Android Wallet is initialized.");
#endif
        }

        /// <summary>
        /// Init object(s).
        /// </summary>
        private void InitObject()
        {
            GraphicContent = new GraphicContent();
            WalletDatabase = new ClassWalletDatabase(this);
            SyncDatabase = new ClassSyncDatabase(this);
            SortingTransaction = new ClassSortingTransaction(this);
            SyncNetwork = new ClassSyncNetwork(this);
            WalletUpdater = new ClassWalletUpdater(this);
            GraphicEvent = new ClassGraphicEvent(this);
            GraphicManager = new GraphicManager(this);
            LanguageObject = new ClassLanguage(this);
        }

        /// <summary>
        /// Init default graphic settings.
        /// </summary>
        private void InitDefaultGraphicSetting()
        {

            if (!_graphicInit)
            {
                Content.RootDirectory = "Content";

                Graphics = new GraphicsDeviceManager(this)
                {
                    IsFullScreen = true,
                    SupportedOrientations = DisplayOrientation.Portrait,
                    GraphicsProfile = GraphicsProfile.HiDef,
                    PreferMultiSampling = false,
                    PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                    PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height
                };
                Graphics.ToggleFullScreen();
                Graphics.ApplyChanges();

                _graphicInit = true;

            }


        }

        /// <summary>
        /// Load each content, graphics content and more.
        /// </summary>
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            LanguageObject.LoadLanguage(Content);
            LanguageObject.LoadEventLanguage(Content);
            GraphicManager.LoadGraphicDesignInfo();
            GraphicManager.LoadTextureContent(GraphicsDevice, Content);
        }


        protected override async void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                Exit();

            if (!ClassUserSetting.WalletSystemBusy)
            {
                await GraphicManager.EnableTouchPadGraphicEventAsync(CurrentMenu);
                GraphicManager.UpdateTransactionHistoryToDraw();
            }

            GraphicManager.AutoCheckCurrentViewportState(GraphicsDevice, Content);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
           

            GraphicsDevice.Clear(Color.White);
            GraphicManager.DrawTexture(_spriteBatch, Color.White);
            base.Draw(gameTime);
        }
    }
}
