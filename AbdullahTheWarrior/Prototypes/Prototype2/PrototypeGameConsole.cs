using System;
using System.Collections.Generic;
using System.Linq;
using DeenGames.AbdullahTheWarrior.Ecs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SadConsole;
using GoRogue.MapViews;
using Troschuetz.Random;
using Troschuetz.Random.Generators;

namespace DeenGames.AbdullahTheWarrior.Prototypes.Prototype2
{
    public class PrototypeGameConsole : SadConsole.Console
    {
        private static readonly int? GameSeed = null;

        public static readonly IGenerator GlobalRandom;

        private readonly Player player;
        private readonly List<Entity> monsters = new List<Entity>();
        private readonly List<AbstractEntity> walls = new List<AbstractEntity>();
        private readonly List<LaserReceptacle> lasers = new List<LaserReceptacle>();
        private readonly List<TeleporterPad> teleporters = new List<TeleporterPad>();
        private int playerTurnsLeftUntilMonsterTurns = 0;

        private readonly int mapHeight;

        private string latestMessage = "";
        private ArrayMap<bool> map;

        private BowManager bow;
        private SwordSkillsManager swordSkillsManager;

        static PrototypeGameConsole() {
            if (!GameSeed.HasValue) {
                GameSeed = new Random().Next();
            }
            
            System.Console.WriteLine($"Universe #{GameSeed.Value}");
            GlobalRandom = new StandardGenerator(GameSeed.Value);
        }

        public PrototypeGameConsole(int width, int height) : base(width, height)
        {
            int playerHealth = 50;
            int playerStrength = 7;
            int playerDefense = 5;
            int playerVision = 6;
            int numberOfTurns = 1;
            int numberOfAttacks = 1;

            this.mapHeight = height - 2;
            this.player = new Player(playerHealth, playerStrength, playerDefense, playerVision, numberOfTurns, numberOfAttacks);

            this.playerTurnsLeftUntilMonsterTurns = player.NumberOfTurns;

            this.bow = new BowManager(this.player);
            this.swordSkillsManager = new SwordSkillsManager(this.player, this.monsters, this.walls);

            this.map = this.GenerateWalls();
            this.GenerateLasers();
            this.GenerateTeleporters();
            this.GenerateMonsters();

            var emptySpot = this.FindEmptySpot();
            player.X = (int)emptySpot.X;
            player.Y = (int)emptySpot.Y;

            this.RedrawEverything();

            EventBus.Instance.AddListener(GameEvent.EntityDeath, (e) => {
                if (e == player)
                {
                    this.bow.Deactivate();
                    this.swordSkillsManager.Deactivate();
                    
                    this.latestMessage = "YOU DIE!!!";
                    this.player.Character = '%';
                    this.player.Color = Palette.Burgandy;

                    this.RedrawEverything();
                }
                else
                {
                    this.monsters.Remove(e as Entity);
                }
            });
        }

        private void GenerateTeleporters()
        {
            const int NUM_TELEPORTERS = 2;
            const int PADS_PER_TELEPORTER = 2;

            while (teleporters.Count < NUM_TELEPORTERS * PADS_PER_TELEPORTER) {
                var location1 = this.FindEmptyLocation(map, monsters, walls, lasers, teleporters);
                var location2 = this.FindEmptyLocation(map, monsters, walls, lasers, teleporters);
                
                var pad1 = new TeleporterPad(location1.Item1, location1.Item2);
                var pad2 = new TeleporterPad(location2.Item1, location2.Item2);
                pad1.Destination = pad2;
                pad2.Destination = pad1;

                this.teleporters.Add(pad1);
                this.teleporters.Add(pad2);
            }
        }

        private Tuple<int, int> FindEmptyLocation(ArrayMap<bool> map, List<Entity> monsters, List<AbstractEntity> walls, List<LaserReceptacle> lasers, List<TeleporterPad> teleporters)
        {
            while (true) {
                var x = PrototypeGameConsole.GlobalRandom.Next(0, map.Width);
                var y = PrototypeGameConsole.GlobalRandom.Next(0, map.Height);

                if (map[x, y] == false && monsters.All(m => m.X != x || m.Y != y) && walls.All(w => w.X != x || w.Y != y) && lasers.All(l => l.X != x || l.Y != y) && teleporters.All(t => t.X != x || t.Y != y)) {
                    return new Tuple<int, int>(x, y);
                }
            }
        }

        private void GenerateLasers()
        {
            var numLasers = PrototypeGameConsole.GlobalRandom.Next(3, 5); //
            for (var i = 0; i < numLasers; i++) {
                // Generates overlapping lasers. That's not a bad thing.
                var location = LaserReceptacle.FindLaserLocation(map, lasers, walls, monsters);
                // horizontal bar
                bool isAlternating = PrototypeGameConsole.GlobalRandom.NextBoolean();
                this.lasers.Add(new LaserReceptacle(location.Item1, location.Item2, Direction.Right, isAlternating));
                this.lasers.Add(new LaserReceptacle(location.Item3, location.Item2, Direction.Left, isAlternating));
            }
        }

        private ArrayMap<bool> GenerateWalls()
        {
            var map = new ArrayMap<bool>(this.Width, this.mapHeight);
            GoRogue.MapGeneration.Generators.CellularAutomataGenerator.Generate(map, PrototypeGameConsole.GlobalRandom, 40);

            for (var y = 0; y < this.mapHeight; y++) {
                for (var x = 0; x < this.Width; x++) {
                    // Invert. We want an internal cave surrounded by walls.
                    map[x, y] = !map[x, y];
                    if (map[x, y]) {
                        this.walls.Add(new AbstractEntity(x, y, '#', Palette.LightGrey)); // FOV determines colour
                    }
                }
            }

            return map;
        }

        public override void Update(System.TimeSpan delta)
        {
            bool playerPressedKey = this.ProcessPlayerInput();

            if (playerPressedKey)
            {
                this.ConsumePlayerTurn();
            }

            // TODO: override Draw and put this in there. And all the infrastructure that requires.
            // Eg. Program.cs must call Draw on the console; and, changing consoles should work.
            this.RedrawEverything();
        }

        private void ConsumePlayerTurn()
        {
            this.playerTurnsLeftUntilMonsterTurns -= 1;

            if (this.playerTurnsLeftUntilMonsterTurns <= 0)
            {
                this.playerTurnsLeftUntilMonsterTurns = player.NumberOfTurns;
                this.ProcessMonsterTurns();
                this.ProcessLasers();

                foreach (var teleporter in this.teleporters) {
                    teleporter.TeleportContents(this.monsters, this.player);
                }

                this.monsters.ForEach(m => m.WasJustTeleported = false);
                this.player.WasJustTeleported = false;
            }
        }

        private void ProcessLasers()
        {
            // Flip lasers on/off
            this.lasers.ForEach(l => l.ProcessTurn());
            // Find the on ones and PEW PEW
            var onLasers = this.lasers.Where(l => l.IsOn);
            onLasers.ToList().ForEach(l => l.Fire(lasers, walls, monsters, this.map.Width, this.mapHeight));
        }

        private void ProcessMonsterTurns()
        {
            foreach (var monster in this.monsters)
            {
                monster.TakeTurn(); // Decrement stun
                
                var distance = Math.Sqrt(Math.Pow(player.X - monster.X, 2) + Math.Pow(player.Y - monster.Y, 2));

                // Monsters who you can see, or hurt monsters, attack.
                if (!monster.IsDead && !monster.IsStunned && (distance <= monster.VisionRange || monster.CurrentHealth < monster.TotalHealth))
                {
                    // Process turn.
                    if (distance <= 1)
                    {
                        // ATTACK~!
                        var damage = AttackResolver.Attacks(monster, player);                        
                        this.latestMessage += $" {monster.Name} attacks for {damage} damage!";
                    }
                    else
                    {
                        // Move closer. Naively. Randomly.
                        var dx = player.X - monster.X;
                        var dy = player.Y - monster.Y;
                        var tryHorizontallyFirst = PrototypeGameConsole.GlobalRandom.Next(0, 100) <= 50;
                        if (tryHorizontallyFirst && dx != 0)
                        {
                            this.TryToMove(monster, monster.X + Math.Sign(dx), monster.Y);
                        }
                        else
                        {
                            this.TryToMove(monster, monster.X, monster.Y + Math.Sign(dy));
                        }
                    }
                }
            }
        }

        private bool TryToMove(Entity entity, int targetX, int targetY)
        {
            // Assuming targetX/targetY are adjacent, or entity can fly/teleport, etc.
            if (this.IsWalkable(targetX, targetY))
            {
                entity.X = targetX;
                entity.Y = targetY;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool ProcessPlayerInput()
        {            
            if (player.IsDead) {
                return false; // don't pass time
            }

            var processedInput = false;

            if (!bow.IsActive && !swordSkillsManager.IsActive)
            {
                if (Global.KeyboardState.IsKeyPressed(Keys.Escape) || Global.KeyboardState.IsKeyPressed(Keys.Q))
                {
                    Environment.Exit(0);
                }
                
                var destinationX = this.player.X;
                var destinationY = this.player.Y;
                
                if ((Global.KeyboardState.IsKeyPressed(Keys.W) || Global.KeyboardState.IsKeyPressed(Keys.Up)))
                {
                    destinationY -= 1;
                }
                else if ((Global.KeyboardState.IsKeyPressed(Keys.S) || Global.KeyboardState.IsKeyPressed(Keys.Down)))
                {
                    destinationY += 1;
                }

                if ((Global.KeyboardState.IsKeyPressed(Keys.A) || Global.KeyboardState.IsKeyPressed(Keys.Left)))
                {
                    destinationX -= 1;
                }
                else if ((Global.KeyboardState.IsKeyPressed(Keys.D) || Global.KeyboardState.IsKeyPressed(Keys.Right)))
                {
                    destinationX += 1;
                }
                
                if (this.TryToMove(player, destinationX, destinationY))
                {
                    processedInput = true;
                    this.latestMessage = "";
                }
                else if (this.GetMonsterAt(destinationX, destinationY) != null)
                {
                    var monster = this.GetMonsterAt(destinationX, destinationY);
                    processedInput = true;

                    var damage = AttackResolver.Attacks(player, monster);
                    var times = player.NumberOfAttacks <= 1 ? "" : $" {player.NumberOfAttacks}x";
                    AttackResolver.ApplyKnockbacks(player, monster, this.monsters, this.walls, damage);
                    this.latestMessage = $"You hit {monster.Name}{times} for {damage} damage!";
                }
                else if (Global.KeyboardState.IsKeyPressed(Keys.OemPeriod) || Global.KeyboardState.IsKeyPressed(Keys.Space))
                {
                    // Skip turn
                    processedInput = true;
                }
                else if (Global.KeyboardState.IsKeyPressed(Keys.F))
                {
                    bow.Activate(this.monsters);
                }

                this.SelectPlayerSkillIfRequired();

                if (player.CurrentHealth <= 0)
                {
                    this.latestMessage = "YOU DIE!!!!";
                }
            }
            else if (bow.IsActive)
            {
                if (Global.KeyboardState.IsKeyPressed(Keys.Tab))
                {
                    bow.RotateTarget();
                }
                else if (Global.KeyboardState.IsKeyPressed(Keys.F))
                {
                    if (bow.HasTarget)
                    {
                        var damage = AttackResolver.Shoots(player, bow.Target);
                        this.latestMessage = $"You shoot an arrow into {bow.Target.Name} afor {damage} damage!";
                        if (bow.Target.CurrentHealth <= 0)
                        {
                            this.bow.Deactivate();
                        }
                        this.ConsumePlayerTurn();
                    }
                    else
                    {
                        this.latestMessage = "No target.";
                    }
                }
                else if (Global.KeyboardState.IsKeyPressed(Keys.Escape))
                {
                    this.bow.Deactivate();
                }
            }
            else if (swordSkillsManager.IsActive)
            {
                swordSkillsManager.ProcessPlayerInput();

                this.SelectPlayerSkillIfRequired();
                if (Global.KeyboardState.IsKeyPressed(Keys.Escape))
                {
                    this.swordSkillsManager.Deactivate();
                }
                else if (Global.KeyboardState.IsKeyPressed(Keys.G)) // HIT!
                {
                    var affectedCells = this.swordSkillsManager.GetSkillTiles();
                    foreach (var cell in affectedCells)
                    {
                        if (this.walls.Any(w => w.X == cell.Item1 && w.Y == cell.Item2))
                        {
                            // We hit a wall. Stop now.
                            break;
                        } else {
                            var monster = this.monsters.SingleOrDefault(m => m.X == cell.Item1 && m.Y == cell.Item2);
                            if (monster != null) {
                                // HURT MONSTER.
                                monster.Damage(swordSkillsManager.GetCurrentSkillDamage());
                            } else {
                                // Blank tile. Move plz.
                                player.X = cell.Item1;
                                player.Y = cell.Item2;                                
                            }
                        }
                    }
                    this.ConsumePlayerTurn();
                }
            }

            return processedInput;
        }

        private void SelectPlayerSkillIfRequired()
        {
            if (Global.KeyboardState.IsKeyPressed(Keys.NumPad1))
            {
                swordSkillsManager.Activate(SwordSkillsManager.Skill.LStrike);
            } else if (Global.KeyboardState.IsKeyPressed(Keys.NumPad2))
            {
                swordSkillsManager.Activate(SwordSkillsManager.Skill.SquareShield);
            } else if (Global.KeyboardState.IsKeyPressed(Keys.NumPad3))
            {
                swordSkillsManager.Activate(SwordSkillsManager.Skill.Wave);
            }
        }

        private void RedrawEverything()
        {
            // One day, I will do better. One day, I will efficiently draw only what changed!
            for (var y = 0; y < this.mapHeight; y++)
            {
                for (var x = 0; x < this.Width; x++)
                {
                    var colour = Palette.Grey;
                    if (IsInPlayerFov(x, y))
                    {
                        colour = Palette.LightGrey;
                    }
                    this.DrawCharacter(x, y, '.', colour);
                }
            }

            foreach (var wall in this.walls)
            {
                var colour = Palette.Grey;
                if (IsInPlayerFov((int)wall.X, (int)wall.Y))
                {
                    colour = wall.Color;
                }
                this.DrawCharacter(wall.X, wall.Y, wall.Character, colour);
            }

            foreach (var monster in this.monsters)
            {                
                if (IsInPlayerFov(monster.X, monster.Y))
                {
                    var character = monster.Character;
                    if (monster.IsStunned)
                    {
                        // If stunned, number of turns stunned. If TOO MANY (like 12), return the last digit (2)
                        if (monster.TurnsStunned >= 10)
                        {
                            character = '9';
                        }
                        else
                        {
                            character = monster.TurnsStunned.ToString().Single();
                        }
                    }
                    this.DrawCharacter(monster.X, monster.Y, character, monster.Color);
                    
                    if (monster.CurrentHealth < monster.TotalHealth) {
                        this.DrawCharacter(monster.X, monster.Y, character, Palette.Orange);
                    }
                }
            }

            foreach (var laser in this.lasers)
            {
                //if (IsInPlayerFov(laser.X, laser.Y)) {
                    this.DrawCharacter(laser.X, laser.Y, laser.Character, laser.Color);

                    foreach (var beam in laser.Beams)
                    {
                        this.DrawCharacter(beam.X, beam.Y, '=', Palette.Blue);
                        if (player.X == beam.X && player.Y == beam.Y)
                        {
                            player.Die();
                        }
                    }
                //}
            }

            foreach (var teleporter in this.teleporters) {
                // IF IN FOV
                this.DrawCharacter(teleporter.X, teleporter.Y, teleporter.Character, teleporter.Color);
            }

            this.DrawCharacter(player.X, player.Y, player.Character, player.Color);

            this.bow.Draw(this);
            this.swordSkillsManager.Draw(this);

            this.DrawLine(new Point(0, this.Height - 2), new Point(this.Width, this.Height - 2), null, Palette.BlackAlmost, ' ');
            this.DrawLine(new Point(0, this.Height - 1), new Point(this.Width, this.Height - 1), null, Palette.BlackAlmost, ' ');
            this.DrawHealthIndicators();
            this.Print(0, this.Height - 1, this.latestMessage, Palette.White);
        }

        private void DrawHealthIndicators()
        {
            string message = $"You: {player.CurrentHealth}/{player.TotalHealth}";
            
            foreach (var monster in this.monsters)
            {
                var distance = Math.Sqrt(Math.Pow(monster.X - player.X, 2) + Math.Pow(monster.Y - player.Y, 2));
                if (distance <= 1 || bow.Target == monster)
                {
                    // compact
                    message = $"{message} {monster.Character}: {monster.CurrentHealth}/{monster.TotalHealth}"; 
                }
            }

            this.Print(1, this.Height - 2, message, Palette.White);
        }

        private bool IsInPlayerFov(int x, int y)
        {
            // Doesn't use LoS calculations, just simple range check
            var distance = Math.Sqrt(Math.Pow(player.X - x, 2) + Math.Pow(player.Y - y, 2));
            return distance <= player.VisionRange;
        }

        private void GenerateMonsters()
        {
            var numMonsters = PrototypeGameConsole.GlobalRandom.Next(80, 90);
            while (this.monsters.Count < numMonsters)
            {
                var spot = this.FindEmptySpot();
                var monster = Entity.CreateFromTemplate("Brigand");
                monster.X = (int)spot.X;
                monster.Y = (int)spot.Y;
                this.monsters.Add(monster);
            }
        }

        private Vector2 FindEmptySpot()
        {
            int targetX = 0;
            int targetY = 0;
            
            do 
            {
                targetX = PrototypeGameConsole.GlobalRandom.Next(0, this.Width);
                targetY = PrototypeGameConsole.GlobalRandom.Next(0, this.mapHeight);
            } while (!this.IsWalkable(targetX, targetY));

            return new Vector2(targetX, targetY);
        }

        private Entity GetMonsterAt(int x, int y)
        {
            // BUG: (secondary?) knockback causes two monsters to occupy the same space!!!
            return this.monsters.FirstOrDefault(m => m.X == x && m.Y == y);
        }

        private bool IsWalkable(int x, int y)
        {
            if (this.walls.Any(w => w.X == x && w.Y == y))
            {
                return false;
            }

            if (this.GetMonsterAt(x, y) != null)
            {
                return false;
            }

            if (this.player.X == x && this.player.Y == y)
            {
                return false;
            }

            return true;
        }
    }
}