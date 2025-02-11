﻿using BenMakesGames.PlayPlayMini.Attributes.DI;
using BenMakesGames.PlayPlayMini.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BenMakesGames.PlayPlayMini.Extensions;

namespace BenMakesGames.PlayPlayMini.Services;

[AutoRegister]
public sealed class GraphicsManager: IServiceLoadContent, IServiceInitialize
{
    private ILogger<GraphicsManager> Logger { get; }

    public bool FullyLoaded { get; private set; }

    public int Zoom { get; private set; } = 2;
    public bool FullScreen { get; private set; }
    public int Width { get; private set; } = 1920 / 3;
    public int Height { get; private set; } = 1080 / 3;

    public int DrawCalls { get; private set; }

    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;
    private ContentManager Content => Game.Content;
    private RenderTarget2D RenderTarget { get; set; } = null!;

    private Game Game { get; set; } = null!;
    private GraphicsDeviceManager Graphics { get; set; } = null!;
    public SpriteBatch SpriteBatch { get; set; } = null!;

    public Dictionary<string, Texture2D> Pictures { get; } = new();
    public Dictionary<string, SpriteSheet> SpriteSheets { get; } = new();
    public Dictionary<string, Font> Fonts { get; } = new();

    public GraphicsManager(ILogger<GraphicsManager> logger)
    {
        Logger = logger;
    }

    public void SetGame(Game game)
    {
        if (Game != null)
            throw new ArgumentException("SetGame can only be called once!");

        Game = game;
        Graphics = new GraphicsDeviceManager(Game);
    }

    public void Initialize(GameStateManager gsm)
    {
        var windowSize = gsm.InitialWindowSize;

        Width = windowSize.Width;
        Height = windowSize.Height;
        Zoom = windowSize.Zoom;

        Graphics.SynchronizeWithVerticalRetrace = false;
        Graphics.PreferredBackBufferWidth = Width * Zoom;
        Graphics.PreferredBackBufferHeight = Height * Zoom;
        Graphics.IsFullScreen = FullScreen;
        Graphics.ApplyChanges();

        SpriteBatch = new SpriteBatch(GraphicsDevice);

        GraphicsDevice.BlendState = BlendState.AlphaBlend;

        RenderTarget = new RenderTarget2D(GraphicsDevice, Width, Height);
    }

    public void LoadContent(GameStateManager gsm)
    {
        Pictures.Clear();
        SpriteSheets.Clear();
        Fonts.Clear();

        // load immediately
        foreach(var meta in gsm.Assets.GetAll<PictureMeta>().Where(m => m.PreLoaded))
            LoadPicture(meta);

        if(!Pictures.ContainsKey("Pixel"))
        {
            var whitePixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            whitePixel.SetData(new[] { Color.White });

            Pictures.Add("Pixel", whitePixel);
        }

        foreach(var meta in gsm.Assets.GetAll<SpriteSheetMeta>().Where(m => m.PreLoaded))
            LoadSpriteSheet(meta);

        foreach(var meta in gsm.Assets.GetAll<FontMeta>().Where(m => m.PreLoaded))
            LoadFont(meta);

        // deferred
        Task.Run(() => LoadDeferredContent(gsm.Assets));
    }

    private void LoadDeferredContent(AssetCollection assets)
    {
        foreach(var meta in assets.GetAll<PictureMeta>().Where(m => !m.PreLoaded))
            LoadPicture(meta);

        foreach(var meta in assets.GetAll<SpriteSheetMeta>().Where(m => !m.PreLoaded))
            LoadSpriteSheet(meta);

        foreach(var meta in assets.GetAll<FontMeta>().Where(m => !m.PreLoaded))
            LoadFont(meta);

        FullyLoaded = true;
    }

    private void LoadFont(FontMeta font)
    {
        try
        {
            Fonts.Add(font.Key, new Font(Content.Load<Texture2D>(font.Path), font.Width, font.Height));
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load {Path}: {Message}", font.Path, e.Message);
        }
    }

    private void LoadPicture(PictureMeta picture)
    {
        try
        {
            Pictures.Add(picture.Key, Content.Load<Texture2D>(picture.Path));
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load {Path}: {Message}", picture.Path, e.Message);
        }
    }

    private void LoadSpriteSheet(SpriteSheetMeta spriteSheet)
    {
        try
        {
            SpriteSheets.Add(spriteSheet.Key, new SpriteSheet(Content.Load<Texture2D>(spriteSheet.Path), spriteSheet.Width, spriteSheet.Height));
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load {Path}: {Message}", spriteSheet.Path, e.Message);
        }
    }

    public void UnloadContent()
    {
        SpriteBatch.Dispose();
    }

    public void SetZoom(int zoom)
    {
        Zoom = zoom < 1 ? 1 : zoom;
        FullScreen = Zoom * Width == Graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Width && Zoom * Height == Graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Height;

        Graphics.SynchronizeWithVerticalRetrace = false;
        Graphics.PreferredBackBufferWidth = Zoom * Width;
        Graphics.PreferredBackBufferHeight = Zoom * Height;
        Graphics.IsFullScreen = FullScreen;
        Graphics.ApplyChanges();
    }

    public void SetFullscreen(bool fullscreen)
    {
        FullScreen = fullscreen;

        int desiredWidth, desiredHeight;

        if (FullScreen)
        {
            desiredWidth = Graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Width;
            desiredHeight = Graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Height;

            Zoom = Math.Min(desiredWidth / Width, desiredHeight / Height);
        }
        else
        {
            desiredWidth = Zoom * Width;
            desiredHeight = Zoom * Height;
        }

        Graphics.SynchronizeWithVerticalRetrace = false;
        Graphics.PreferredBackBufferWidth = desiredWidth;
        Graphics.PreferredBackBufferHeight = desiredHeight;
        Graphics.IsFullScreen = FullScreen;
        Graphics.ApplyChanges();
    }

    public int MaxZoom()
    {
        return Math.Min(
            Graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Width / Width,
            Graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Height / Height
        );
    }

    public void BeginDraw()
    {
        DrawCalls = 0;

        GraphicsDevice.SetRenderTarget(RenderTarget);
        SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(Color c)
        => GraphicsDevice.Clear(c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rectangle SpriteRectangle(SpriteSheet ss, int spriteIndex) => new(
        (spriteIndex % ss.Columns) * ss.SpriteWidth,
        (spriteIndex / ss.Columns) * ss.SpriteHeight,
        ss.SpriteWidth,
        ss.SpriteHeight
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Rectangle FontRectangle(Font font, int character) => new(
        (character % font.Columns) * font.CharacterWidth,
        (character / font.Columns) * font.CharacterHeight,
        font.CharacterWidth,
        font.CharacterHeight
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawFilledRectangle((int x, int y) upperLeft, (int x, int y) bottomRight, Color c)
    {
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(upperLeft.x, upperLeft.y, bottomRight.x - upperLeft.x + 1, bottomRight.y - upperLeft.y + 1), null, c);
        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawFilledRectangle(int x, int y, int w, int h, Color c)
    {
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(x, y, w, h), null, c);
        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawFilledRectangle(int x, int y, int w, int h, Color fill, Color outline)
    {
        DrawFilledRectangle(x + 1, y + 1, w - 2, h - 2, fill);
        DrawRectangle(x, y, w, h, outline);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPoints(IEnumerable<(int x, int y)> points, Color c)
    {
        foreach (var p in points)
            DrawPoint(p.x, p.y, c);
    }

    public void DrawRectangle(int x, int y, int w, int h, Color c)
    {
        // top & bottom line
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(x, y, w, 1), null, c);
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(x, y + h - 1, w, 1), null, c);

        // left & right line
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(x, y + 1, 1, h - 2), null, c);
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(x + w - 1, y + 1, 1, h - 2), null, c);

        DrawCalls += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPoint(int x, int y, Color c)
    {
        SpriteBatch.Draw(Pictures["Pixel"], new Rectangle(x, y, 1, 1), c);
        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPictureRotatedAndScaled(string pictureName, int x, int y, float angle, float scale, Color c)
        => DrawTextureRotatedAndScaled(Pictures[pictureName], x, y, angle, scale, c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureRotatedAndScaled(Texture2D texture, int x, int y, float angle, float scale, Color c)
    {
        SpriteBatch.Draw(
            texture,
            new Rectangle(x, y, (int)(texture.Width * scale), (int)(texture.Height * scale)),
            null,
            c,
            angle,
            new Vector2(texture.Width / 2, texture.Height / 2),
            SpriteEffects.None,
            0
        );

        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPictureWithTransformations(string pictureName, int x, int y, Rectangle spriteRectangle, SpriteEffects flip, float angle, float scale, Color c) =>
        DrawTextureWithTransformations(Pictures[pictureName], x, y, spriteRectangle, flip, angle, scale, scale, c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureWithTransformations(Texture2D texture, int x, int y, Rectangle spriteRectangle, SpriteEffects flip, float angle, float scaleX, float scaleY, Color c)
    {
        SpriteBatch.Draw(
            texture,
            new Rectangle(x, y, (int)(spriteRectangle.Width * scaleX), (int)(spriteRectangle.Height * scaleY)),
            spriteRectangle,
            c,
            angle,
            new Vector2(spriteRectangle.Width / 2, spriteRectangle.Height / 2),
            flip,
            0
        );

        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteWithTransformations(SpriteSheet ss, int x, int y, int spriteIndex, SpriteEffects flip, float angle, float scale, Color tint)
        => DrawTextureWithTransformations(
            ss.Texture,
            x, y,
            SpriteRectangle(ss, spriteIndex),
            flip,
            angle,
            scale,
            scale,
            tint
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPictureRotatedAndScaled(string pictureName, int x, int y, Rectangle spriteRectangle, float angle, float scale, Color c)
        => DrawTextureRotatedAndScaled(Pictures[pictureName], x, y, spriteRectangle, angle, scale, c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureRotatedAndScaled(Texture2D texture, int x, int y, Rectangle spriteRectangle, float angle, float scale, Color c)
        => DrawTextureWithTransformations(
            texture,
            x,
            y,
            spriteRectangle,
            SpriteEffects.None,
            angle,
            scale,
            scale,
            c
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRotatedAndScaled(string spriteSheet, int x, int y, int spriteIndex, float angle, float scale, Color c) =>
        DrawTextureWithTransformations(
            SpriteSheets[spriteSheet].Texture,
            x,
            y,
            SpriteRectangle(SpriteSheets[spriteSheet], spriteIndex),
            SpriteEffects.None,
            angle,
            scale,
            scale,
            c
        )
    ;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteRotatedAndScaled(SpriteSheet ss, int x, int y, int spriteIndex, float angle, float scale, Color c) =>
        DrawTextureWithTransformations(
            ss.Texture,
            x,
            y,
            SpriteRectangle(ss, spriteIndex),
            SpriteEffects.None,
            angle,
            scale,
            scale,
            c
        )
    ;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int x, int y) DrawText(string name, int x, int y, string text, Color color)
        => DrawText(Fonts[name], x, y, text, color);

    public (int x, int y) DrawText(Font font, int x, int y, string text, Color color)
    {
        var position = (x, y);

        foreach(char c in text)
        {
            if(c == 10 || c == 13)
            {
                position.x = x;
                position.y += font.CharacterHeight + 1;
            }
            else
                position = DrawText(font, position.x, position.y, c, color);
        }

        return position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int x, int y) DrawText(string name, int x, int y, char character, Color color)
        => DrawText(Fonts[name], x, y, character, color);

    public (int x, int y) DrawText(Font font, int x, int y, char character, Color color)
    {
        (int x, int y) position = (x, y);

        if (character >= 32)
        {
            DrawTexture(font.Texture, position.x, position.y, FontRectangle(font, character - 32), color);

            position.x += font.CharacterWidth;
        }
        else if (character == 10 || character == 13)
        {
            position.x = x;
            position.y += font.CharacterHeight + 1;
        }

        return position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int, int) ComputeDimensionsWithWordWrap(string name, int maxWidth, string text)
        => ComputeDimensionsWithWordWrap(Fonts[name], maxWidth, text);

    public (int, int) ComputeDimensionsWithWordWrap(Font font, int maxWidth, string text)
    {
        var lines = text.Split(new char[] { '\r', '\n' });

        int pixelX, pixelY = 0;
        int maxPixelX = 0;
        bool first = true;

        foreach (string line in lines)
        {
            pixelX = 0;

            if (first)
                first = false;
            else
                pixelY += font.CharacterHeight + 1;

            var words = line.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0)
                    pixelX += font.CharacterWidth;

                string word = words[i];

                if (pixelX + word.Length * font.CharacterWidth > maxWidth)
                {
                    pixelX = 0;
                    pixelY += font.CharacterHeight + 1;
                }

                if (pixelX > maxPixelX)
                    maxPixelX = pixelX;

                (pixelX, pixelY) = PretendDrawText(font, pixelX, pixelY, word);

                if (pixelX > maxPixelX)
                    maxPixelX = pixelX;
            }
        }

        return (maxPixelX, pixelY + font.CharacterHeight);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int, int) DrawTextWithWordWrap(string fontName, int x, int y, int maxWidth, string text, Color color)
        => DrawTextWithWordWrap(Fonts[fontName], x, y, maxWidth, text, color);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int, int) DrawTextWithWordWrap(Font font, int x, int y, int maxWidth, string text, Color color)
        => DrawText(font, x, y, text.WrapText(font, maxWidth), color);

    public (int, int) PretendDrawText(Font font, int x, int y, string text)
    {
        int currentX = x;
        int currentY = y;

        foreach (char c in text)
        {
            if (c >= 32)
                currentX += font.CharacterWidth;
            else if (c == 10 || c == 13)
            {
                currentX = x;
                currentY += font.CharacterHeight + 1;
            }
        }

        return (currentX, currentY);
    }

    public void DrawTextWithOutline(Font font, Font outlineFont, int x, int y, string text, Color fill, Color outline)
    {
        int currentX = x;
        int currentY = y;

        foreach (char c in text)
        {
            if (c >= 32)
            {
                DrawTexture(outlineFont.Texture, currentX - 1, currentY - 1, FontRectangle(outlineFont, c - 32), outline);

                currentX += font.CharacterWidth;
            }
            else if (c == 10 || c == 13)
            {
                currentX = x;
                currentY += font.CharacterHeight + 1;
            }
        }

        currentX = x;
        currentY = y;

        DrawText(font, currentX, currentY, text, fill);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(string name, int x, int y, int spriteIndex)
        => DrawSprite(SpriteSheets[name], x, y, spriteIndex, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(string name, (int x, int y) position, int spriteIndex)
        => DrawSprite(SpriteSheets[name], position.x, position.y, spriteIndex, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(SpriteSheet ss, int x, int y, int spriteIndex)
        => DrawSprite(ss, x, y, spriteIndex, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(SpriteSheet ss, (int x, int y) position, int spriteIndex)
        => DrawSprite(ss, position.x, position.y, spriteIndex, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(string name, int x, int y, int spriteIndex, Color tint)
        => DrawSprite(SpriteSheets[name], x, y, spriteIndex, tint);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(string name, (int x, int y) position, int spriteIndex, Color tint)
        => DrawSprite(SpriteSheets[name], position.x, position.y, spriteIndex, tint);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(SpriteSheet ss, (int x, int y) position, int spriteIndex, Color tint)
        => DrawSprite(ss, position.x, position.y, spriteIndex, tint);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(SpriteSheet ss, int x, int y, int spriteIndex, Color tint)
    {
        DrawTexture(
            ss.Texture,
            x, y,
            SpriteRectangle(ss, spriteIndex),
            tint
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPicture(string pictureName, int x, int y)
        => DrawTexture(Pictures[pictureName], x, y, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPicture(string pictureName, int x, int y, Color tint)
        => DrawTexture(Pictures[pictureName], x, y, tint);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTexture(Texture2D texture, int x, int y)
        => DrawTexture(texture, x, y, new Rectangle(0, 0, texture.Width, texture.Height), Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTexture(Texture2D texture, int x, int y, Color color)
        => DrawTexture(texture, x, y, new Rectangle(0, 0, texture.Width, texture.Height), color);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTexture(Texture2D texture, int x, int y, Rectangle spriteRectangle)
        => DrawTexture(texture, x, y, spriteRectangle, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTexture(Texture2D texture, int x, int y, Rectangle spriteRectangle, Color color)
    {
        SpriteBatch.Draw(texture, new Vector2(x, y), spriteRectangle, color);
        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPictureStretched(string pictureName, int x, int y, int width, int height, Rectangle clippingRectangle)
        => DrawTextureStretched(Pictures[pictureName], x, y, width, height, clippingRectangle, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawPictureStretched(string pictureName, int x, int y, int width, int height, Rectangle clippingRectangle, Color c) =>
        DrawTextureStretched(Pictures[pictureName], x, y, width, height, clippingRectangle, c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureStretched(Texture2D texture, int x, int y, int width, int height, Rectangle spriteRectangle) =>
        DrawTextureStretched(texture, x, y, width, height, spriteRectangle, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureStretched(Texture2D texture, int x, int y, int width, int height, Rectangle spriteRectangle, Color c)
    {
        SpriteBatch.Draw(texture, new Rectangle(x, y, width, height), spriteRectangle, c);
        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteStretched(SpriteSheet ss, int x, int y, int width, int height, int spriteIndex) =>
        DrawTextureStretched(
            ss.Texture,
            x, y,
            width, height,
            SpriteRectangle(ss, spriteIndex),
            Color.White
        )
    ;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureFlipped(Texture2D texture, int x, int y, SpriteEffects flip) =>
        DrawTextureFlipped(texture, x, y, new Rectangle(0, 0, texture.Width, texture.Height), flip, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureFlipped(Texture2D texture, int x, int y, SpriteEffects flip, Color tint) =>
        DrawTextureFlipped(texture, x, y, new Rectangle(0, 0, texture.Width, texture.Height), flip, tint);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureFlipped(Texture2D texture, int x, int y, Rectangle clippingRectangle, SpriteEffects flip) =>
        DrawTextureFlipped(texture, x, y, clippingRectangle, flip, Color.White);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawTextureFlipped(Texture2D texture, int x, int y, Rectangle clippingRectangle, SpriteEffects flip, Color tint)
    {
        SpriteBatch.Draw(texture, new Rectangle(x, y, clippingRectangle.Width, clippingRectangle.Height), clippingRectangle, tint, 0, Vector2.Zero, flip, 0);
        DrawCalls++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteFlipped(string spriteSheetName, int x, int y, int spriteIndex, SpriteEffects flip) =>
        DrawTextureFlipped(
            SpriteSheets[spriteSheetName].Texture,
            x,
            y,
            SpriteRectangle(SpriteSheets[spriteSheetName], spriteIndex),
            flip,
            Color.White
        )
    ;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteFlipped(SpriteSheet ss, int x, int y, int spriteIndex, SpriteEffects flip) =>
        DrawTextureFlipped(
            ss.Texture,
            x,
            y,
            SpriteRectangle(ss, spriteIndex),
            flip,
            Color.White
        )
    ;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteFlipped(SpriteSheet ss, int x, int y, int spriteIndex, SpriteEffects flip, Color tint) =>
        DrawTextureFlipped(
            ss.Texture,
            x,
            y,
            SpriteRectangle(ss, spriteIndex),
            flip,
            tint
        )
    ;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSpriteFlipped(string spriteName, int x, int y, int spriteIndex, SpriteEffects flip, Color tint) =>
        DrawTextureFlipped(
            SpriteSheets[spriteName].Texture,
            x,
            y,
            SpriteRectangle(SpriteSheets[spriteName], spriteIndex),
            flip,
            tint
        );

    public void EndDraw()
    {
        SpriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);

        SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
        SpriteBatch.Draw(RenderTarget, new Rectangle(0, 0, Width * Zoom, Height * Zoom), Color.White);
        SpriteBatch.End();
    }
}
