using System;
using System.Collections.Generic;
using System.Linq;
using DeenGames.AbdullahTheWarrior.Ecs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SadConsole;

namespace DeenGames.AbdullahTheWarrior.Prototype
{
    public class MainMenuConsole : SadConsole.Console
    {
        private bool isSelectingClass = false;

        public MainMenuConsole(int width, int height) : base(width, height)
        {
            int startX = width / 2;
            int startY = height / 2;
            this.Print(startX, startY, "[N]ew Game");
        }

        public override void Update(System.TimeSpan delta)
        {
            if (!isSelectingClass && Global.KeyboardState.IsKeyReleased(Keys.N))
            {
                this.isSelectingClass = true;
                this.ShowClassSelections();
            }
            else if (isSelectingClass && Global.KeyboardState.KeysReleased.Any())
            {
                if (Global.KeyboardState.IsKeyReleased(Keys.A))
                {
                    this.StartGameAs("Faris");
                }
            }
        }

        private void StartGameAs(string className)
        {
            Global.CurrentScreen.Children.Clear();
            Global.CurrentScreen.Children.Add(new PrototypeGameConsole(this.Width, this.Height));
        }

        private void ShowClassSelections()
        {
            this.Clear();

            int startX = 8;
            int startY = this.Height / 2;

            this.Print(startX, startY, "[A] Faris (multiple turns, multiple hits per attack)");
        }
    }
}