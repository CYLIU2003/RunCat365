// Copyright 2025 Takuto Nakamura
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using RunCat365.Properties;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunCat365
{
    internal class EndlessGameForm : Form
    {
        private const int JUMP_THRESHOLD = 17;
        private const int VISIBLE_ROAD_COUNT = 20;
        private static readonly string[] RoadResourceNames = ["road_flat", "road_hill", "road_crater", "road_sprout"];

        private readonly FormsTimer timer;
        private readonly Font scoreFont = new("Consolas", 15);
        private readonly Font overlayFont = new("Segoe UI", 16, FontStyle.Bold);
        private readonly SolidBrush textBrush;
        private readonly SolidBrush overlayFillBrush = new(Color.FromArgb(77, 0, 0, 0));
        private readonly StringFormat scoreStringFormat = new()
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center
        };
        private readonly StringFormat overlayStringFormat = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private readonly Bitmap?[] runningCatBitmaps = new Bitmap?[5];
        private readonly Bitmap?[] jumpingCatBitmaps = new Bitmap?[10];
        private readonly Bitmap?[] roadBitmaps = new Bitmap?[4];
        private readonly List<Road> roads = new(24);
        private GameStatus status = GameStatus.NewGame;
        private Cat cat = Cat.Running.FromFrame(Cat.Running.Frame.Frame0);
        private int counter = 0;
        private int limit = 5;
        private int score = 0;
        private int highScore = UserSettings.Default.HighScore;
        private bool isNewRecord = false;
        private bool isJumpRequested = false;
        private readonly bool isAutoPlay = false;
        private int cachedScore = -1;
        private int cachedHighScore = -1;
        private GameStatus cachedScoreStatus = (GameStatus)(-1);
        private string cachedScoreText = "000";

        internal EndlessGameForm(Theme systemTheme)
        {
            DoubleBuffered = true;
            ClientSize = new Size(600, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = Strings.Window_EndlessGame;
            Icon = Resources.AppIcon;
            BackColor = systemTheme == Theme.Light ? Color.Gainsboro : Color.Gray;

            var color = systemTheme.GetContrastColor();
            textBrush = new SolidBrush(color);

            var rm = Resources.ResourceManager;
            for (int i = 0; i < runningCatBitmaps.Length; i++)
            {
                runningCatBitmaps[i] = LoadThemedBitmap(rm, $"cat_running_{i}", systemTheme, color);
            }
            for (int i = 0; i < jumpingCatBitmaps.Length; i++)
            {
                jumpingCatBitmaps[i] = LoadThemedBitmap(rm, $"cat_jumping_{i}", systemTheme, color);
            }
            for (int i = 0; i < roadBitmaps.Length; i++)
            {
                roadBitmaps[i] = LoadThemedBitmap(rm, RoadResourceNames[i], systemTheme, color);
            }

            Paint += RenderScene;

            KeyDown += HandleKeyDown;

            timer = new FormsTimer
            {
                Interval = 100
            };
            timer.Tick += GameTick;

            Initialize();
        }

        private static Bitmap? LoadThemedBitmap(
            System.Resources.ResourceManager resourceManager,
            string resourceName,
            Theme systemTheme,
            Color contrastColor
        )
        {
            if (resourceManager.GetObject(resourceName) is not Bitmap bitmap) return null;
            return systemTheme == Theme.Light ? new Bitmap(bitmap) : bitmap.Recolor(contrastColor);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            timer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();

                scoreFont.Dispose();
                overlayFont.Dispose();
                textBrush.Dispose();
                overlayFillBrush.Dispose();
                scoreStringFormat.Dispose();
                overlayStringFormat.Dispose();

                DisposeBitmaps(runningCatBitmaps);
                DisposeBitmaps(jumpingCatBitmaps);
                DisposeBitmaps(roadBitmaps);
            }
            base.Dispose(disposing);
        }

        private static void DisposeBitmaps(Bitmap?[] bitmaps)
        {
            for (int i = 0; i < bitmaps.Length; i++)
            {
                bitmaps[i]?.Dispose();
                bitmaps[i] = null;
            }
        }

        private void Initialize()
        {
            counter = JUMP_THRESHOLD;
            isJumpRequested = false;
            isNewRecord = false;
            score = 0;
            cat = Cat.Running.FromFrame(Cat.Running.Frame.Frame0);
            roads.RemoveAll(r => r == Road.Sprout);
            while (roads.Count < VISIBLE_ROAD_COUNT)
            {
                roads.Add((Road)Random.Shared.Next(0, 3));
            }
        }

        private bool Judge()
        {
            if (status != GameStatus.Playing) return false;
            var violationIndices = cat.ViolationIndices();
            for (int i = 0; i < violationIndices.Length; i++)
            {
                var index = violationIndices[i];
                if ((uint)index < (uint)roads.Count && roads[index] == Road.Sprout)
                {
                    status = GameStatus.GameOver;
                    isNewRecord = score >= highScore;
                    if (isNewRecord)
                    {
                        SaveRecord(score);
                    }
                    timer.Stop();
                    return false;
                }
            }
            return true;
        }

        private void UpdateRoads()
        {
            var firstRoad = roads[0];
            roads.RemoveAt(0);
            if (firstRoad == Road.Sprout)
            {
                score = Math.Min(score + 1, 999);
                highScore = Math.Max(score, highScore);
            }
            counter = counter > 0 ? counter - 1 : limit - 1;
            if (counter == 0)
            {
                var randomValue = Random.Shared.Next(0, 27);
                int addedSprouts = 0;
                if (randomValue % 3 == 0)
                {
                    roads.Add(Road.Sprout);
                    addedSprouts++;
                }
                if (randomValue % 9 == 0)
                {
                    roads.Add(Road.Sprout);
                    addedSprouts++;
                }
                if (randomValue % 27 == 0)
                {
                    roads.Add(Road.Sprout);
                    addedSprouts++;
                }
                limit = addedSprouts == 0 ? 5 : 10;
            }
            if (roads.Count < VISIBLE_ROAD_COUNT)
            {
                roads.Add((Road)Random.Shared.Next(0, 3));
            }
        }

        private void UpdateCat()
        {
            if (cat is Cat.Running runningCat)
            {
                if (runningCat.CurrentFrame == Cat.Running.Frame.Frame4 && isJumpRequested)
                {
                    cat = Cat.Jumping.FromFrame(Cat.Jumping.Frame.Frame0);
                    isJumpRequested = false;
                    return;
                }
            }
            else if (cat is Cat.Jumping jumpingCat)
            {
                if (jumpingCat.CurrentFrame == Cat.Jumping.Frame.Frame9)
                {
                    if (isJumpRequested)
                    {
                        cat = cat.Next();
                        isJumpRequested = false;
                        return;
                    }
                    else
                    {
                        cat = Cat.Running.FromFrame(Cat.Running.Frame.Frame0);
                        return;
                    }
                }
            }
            cat = cat.Next();
        }

        private void AutoJump()
        {
            if (isAutoPlay && roads[JUMP_THRESHOLD - 1] == Road.Sprout)
            {
                isJumpRequested = true;
            }
        }

        private void GameTick(object? sender, EventArgs e)
        {
            if (Judge())
            {
                UpdateRoads();
                UpdateCat();
                AutoJump();
            }
            Invalidate();
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                switch (status)
                {
                    case GameStatus.NewGame:
                    case GameStatus.GameOver:
                        Initialize();
                        status = GameStatus.Playing;
                        timer.Start();
                        Invalidate();
                        break;
                    case GameStatus.Playing when !isAutoPlay:
                        isJumpRequested = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private string GetScoreText()
        {
            if (cachedScore != score || cachedHighScore != highScore || cachedScoreStatus != status)
            {
                cachedScore = score;
                cachedHighScore = highScore;
                cachedScoreStatus = status;
                cachedScoreText = status == GameStatus.GameOver
                    ? $"HI {highScore:D3}  {score:D3}"
                    : $"{score:D3}";
            }
            return cachedScoreText;
        }

        private void RenderScene(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;

            g.DrawString(GetScoreText(), scoreFont, textBrush, new Rectangle(20, 0, 560, 50), scoreStringFormat);

            var visibleCount = Math.Min(roads.Count, VISIBLE_ROAD_COUNT);
            for (int i = 0; i < visibleCount; i++)
            {
                var roadIndex = (int)roads[i];
                if ((uint)roadIndex < (uint)roadBitmaps.Length && roadBitmaps[roadIndex] is Bitmap roadImage)
                {
                    g.DrawImage(roadImage, new Rectangle(i * 30, 200, 30, 50));
                }
            }

            Bitmap? catImage = cat switch
            {
                Cat.Running running => runningCatBitmaps[(int)running.CurrentFrame],
                Cat.Jumping jumping => jumpingCatBitmaps[(int)jumping.CurrentFrame],
                _ => null
            };
            if (catImage is not null)
            {
                g.DrawImage(catImage, new Rectangle(120, 130, 120, 100));
            }

            if (status != GameStatus.Playing)
            {
                g.FillRectangle(overlayFillBrush, new Rectangle(0, 0, 600, 250));

                var message = Strings.Game_PressSpaceToPlay;
                if (status == GameStatus.GameOver)
                {
                    message = isNewRecord
                        ? $"{Strings.Game_NewRecord}!!\n{message}"
                        : $"{Strings.Game_GameOver}\n{message}";
                }
                g.DrawString(message, overlayFont, textBrush, new Rectangle(0, 0, 600, 250), overlayStringFormat);
            }
        }

        private static void SaveRecord(int score)
        {
            UserSettings.Default.HighScore = score;
            UserSettings.Default.Save();
        }
    }
}
