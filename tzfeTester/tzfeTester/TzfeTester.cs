using System;
using tzfeGameEngine;

namespace tzfeTester
{
    class Program {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello We are able to test the engine");
            GameRunner runner = new GameRunner();
            runner.Play();
        }
    }

    class GameRunner : ITzfeGameDelegate {
    
		// define some strings
		private const string clearScrn = "clear";
		private const string prompt = "\n\t*** Let's PLAY!! ***\n\tActions:[ arrows(left, right, up, down), n=new game, q=quit game]: ";
		private const string nilSlideLeft = " - CAN'T SLIDE OR COMPACT ANY MORE LEFT!";
		private readonly string nilSlideRight = " - CAN'T SLIDE OR COMPACT ANY MORE RIGHT!";
		private readonly string nilSlideUp = " - CAN'T SLIDE OR COMPACT ANY MORE UP!";
		private readonly string nilSlideDown = " - CAN'T SLIDE OR COMPACT ANY MORE DOWN!";
		private readonly string invalidAction = "invalid action. Please try again!";
		private readonly string quitMsg = "\n\n\t*** Thanks for playing :)  Come again! ***\n\n";
		private readonly string youLose = "\n\t*** GAME OVER -> YOU LOSE. NO MOVES REMAINING. PRESS ENTER FOR NEW GAME: ***\n";
		private readonly string youWin = "\n\t*** YOU DA WINNER  !!!AWESOME!!!  PRESS ENTER FOR NEW GAME: ***\n";
		private readonly string yourPb = "\n\t*** YOU have hit another PB !!!AWESOME!!!: ***\n";
		private readonly string undoFailed = "\n\n\t*** Sorry!! No back moves available!!!: ***\n";

		private bool mUseAi = false;
		private int mPrevHiScore = 0;
        private int mSize = -1;
        private TzfeGameEngine mGame;
		private GameMoves mLastFailedMove;

		ConsoleColor savedBackColor = Console.BackgroundColor;
		ConsoleColor savedForeColor = Console.ForegroundColor;

		public GameRunner() {}

        public void Play() {

			this.ClearScreen();

			string answer = null;

            var date = DateTime.Now;
            Console.WriteLine("\nRighto!!. Let's play TZFE (2048)\n");

            while (true) {
                bool playSameAsBefore = false;
				if (mSize > 1) {
					Console.Write("Would you like to replay the same as before, New, or Quit: [Y/n/q]?: ");
					answer = Console.ReadLine() ?? "y";
					answer = answer.Length > 0 ? answer : "y";
					if (answer == "q") return;
					playSameAsBefore = (answer == "y");
				} 

				if (!playSameAsBefore) {
					Console.Write("What size panel do you want?: Default = (4)?: ");
					answer = Console.ReadLine() ?? "4";
					answer = answer.Length > 0 ? answer : "4";
					mSize = Int32.Parse(answer);

					Console.Write("Would you like to play yourself or run AI: [AI/self]?: ");
					answer = Console.ReadLine() ?? "a";
					answer = answer.Length > 0 ? answer.ToLower() : "a";
					mUseAi = (answer.Length == 0 || answer == "a" || answer == "ai");
				}

				// Construct new Game.
				mGame = new TzfeGameEngine(this, mSize);
				mGame.NewGame(mPrevHiScore);

				Console.Write("\nOky Doky!!. Running panel size {0}, using AI = {1}.\nPress space or other key to continue, else 'Q' to Quit: ", mSize, mUseAi);
				answer = Console.ReadLine() ?? "";
				answer = answer.Length > 0 ? answer.ToLower() : "";
				if (answer == "q") {
					RestoreScreen();
					return;
				}

				this.PlayGame(); // play this game.
			}
        }

		private void PlayGame() {

			ClearScreen();
			var action = "\n";

			while (mGame.HasMovesRemaining()) {

				// Clear screen and output updated game board resulting from last input action.
				this.ClearScreen();
				Console.WriteLine(mGame.AsString());
				Console.WriteLine(action);
				Console.Write(prompt);

				var input = Console.ReadKey();  // get unbuffered user input.
				action = "\n\tLast action: ";

				switch (input.Key) {
					case ConsoleKey.LeftArrow:
						action += "move left";
						if (!mGame.ActionMove(GameMoves.Left)) action += nilSlideLeft;
						break;
					case ConsoleKey.RightArrow:
						action += "move right";
						if (!mGame.ActionMove(GameMoves.Right)) action += nilSlideRight;
						break;
					case ConsoleKey.UpArrow:
						action += "move up";
						if (!mGame.ActionMove(GameMoves.Up)) action += nilSlideUp;
						break;
					case ConsoleKey.DownArrow:
						action += "move down";
						if (!mGame.ActionMove(GameMoves.Down)) action += nilSlideDown;
						break;
					case ConsoleKey.N:
						mGame.NewGame(mPrevHiScore);
						action += "start new game";
						break;
					case ConsoleKey.B:
						mGame.GoBackOneMove();
						action += "go back one move";
						break;
					case ConsoleKey.Q:
						this.ClearScreen();
						Console.WriteLine(mGame.AsString());
						action += "quit current game";
						action += quitMsg;
						Console.WriteLine(action);
						return;  // Exit the game program here.
					default:
						action += invalidAction;
						break;
				}
			}
		}

		private void RestoreScreen() {
			Console.BackgroundColor = savedBackColor;
			Console.ForegroundColor = savedForeColor;
			Console.Clear();
		}

		private void ClearScreen() {
			Console.BackgroundColor = ConsoleColor.DarkBlue;
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.Clear();
		}

		private void RenderPanel() {
			var panelStr = mGame.AsString();
		}

		// TZFE Interface Methods
		public void UpdateTileValue(Transition move) {
			// Can be used to animate all tile movements.
        }

        public void UserFail() {
			Console.WriteLine(youLose);
        }

        public void UserPB(int score) {
			Console.WriteLine(yourPb);
        }

        public void UserScoreChanged(int score) {
			if (score > mPrevHiScore) mPrevHiScore = score;
        }

        public void UserWin() {
			Console.WriteLine(youWin);
		}

		public void UndoRequestFail() {
			Console.WriteLine(undoFailed);
		}

		public void MoveRequestOutcome(GameMoves move, bool success) {
			if (!success) mLastFailedMove = move;
			else mLastFailedMove = GameMoves.Rest;
		}
	}

}
