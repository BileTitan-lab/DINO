using System.Drawing.Imaging;
using DinoDash.Desktop;

internal static class RestVisualChecks
{
    public static void Run()
    {
        foreach (int chapter in new[] { 1, 2, 3, 4 })
        {
            var game = new ActEngine(17);
            game.LoadMapForDebug(chapter, 17, false);
            game.EnterRestStop(37 + chapter);
            if (game.RestStop!.Facilities.All(f => f.Kind != RestFacilityKind.Merchant))
            {
                game.RestStop.Facilities.Add(new RestFacility(RestFacilityKind.Merchant, 1380));
                game.RestStop.Stock.Add(new(ShopKind.Art,50,BattleArtId.BoneArmor));
                game.RestStop.Stock.Add(new(ShopKind.Healing,35));
                game.RestStop.Stock.Add(new(ShopKind.Evolution,110,evolution:EvolutionId.HollowBones));
            }
            game.PlacePlayerForTest(525, 248);
            using var canvas = new GameCanvas(game) { Size = new Size(1280, 720), Dimmed = false };
            canvas.UpdateHud(true);
            using var picture = new Bitmap(1280, 720);
            canvas.DrawToBitmap(picture, new Rectangle(0, 0, 1280, 720));
            picture.Save(Path.Combine(AppContext.BaseDirectory, $"rest-chapter-{chapter}.png"), ImageFormat.Png);
            game.PlacePlayerForTest(game.RestStop.Facilities.First(f => f.Kind == RestFacilityKind.Merchant).X - 27, 248);
            if (!game.InteractWithRest() || !game.ShopRequested)
                throw new Exception($"Merchant interaction failed in visual check: chapter={chapter} x={game.PlayerX} near={game.NearbyFacility?.Kind} complete={game.IsComplete} shop={game.ShopRequested}");
            using var shop = new ActRestShopDialog(game);
            if (shop.Controls.OfType<Panel>().Count() < 3)
                throw new Exception("Merchant does not show three offers");
            if (chapter == 1)
            {
                var cards = shop.Controls.OfType<Panel>().Where(p => p.Controls.OfType<Button>().Any()).ToArray();
                using var shopImage = new Bitmap(cards[0].Width, cards[0].Height);
                cards[0].Parent = null;
                cards[0].DrawToBitmap(shopImage, new Rectangle(Point.Empty, shopImage.Size));
                shopImage.Save(Path.Combine(AppContext.BaseDirectory, "rest-shop-card.png"), ImageFormat.Png);
            }
        }
        Console.WriteLine("PASS four chapter rest looks, merchant interaction and three-offer shop rendered offscreen.");
    }
}
