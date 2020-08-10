using System;
using System.Threading.Tasks;
using tzfeGameEngine;

namespace tzfeTester {
	class Program {
		static async Task Main(string[] args) {
			Console.WriteLine("Hello We are able to test the engine");
			GameRunner runner = new GameRunner();
			await runner.PlayAsync();
		}
	}

	class GameRunner : ITzfeGameDelegate {

		// define some strings
		private const string clearScrn = "clear";
		private const string prompt = "\n\t*** Let's PLAY!! ***\n\tActions:[arrows(left,right,up,down), n=new game, q=quit game]: ";
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
		private GameMoves mLastMove = GameMoves.Empty;
		private bool mLastMoveOk = true;
		private bool mUndoRequestFailed = false;

		readonly ConsoleColor savedBackColor = Console.BackgroundColor;
		readonly ConsoleColor savedForeColor = Console.ForegroundColor;

		public GameRunner() { }

		public async Task PlayAsync() {

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

				Console.Write("\nOky Doky!!. Running panel size {0}, using AI = {1}.\nPress enter to continue, else 'Q' to Quit: ", mSize, mUseAi);
				if (Console.ReadKey().Key == ConsoleKey.Q) {
					RestoreScreen();
					return;
				}

				await this.PlayGameAsync(); // play this game.
			}
		}

		private async Task PlayGameAsync() {

			ClearScreen();
			var action = "\n";

			while (mGame.HasMovesRemaining()) {

				// Clear screen and output updated game board resulting from last input action.
				ClearScreen();
				RenderPanel();
				Console.WriteLine(action);
				Console.Write(prompt);

				if (mUndoRequestFailed) {
					Console.WriteLine(undoFailed);
					mUndoRequestFailed = false;
				}

				ConsoleKey input; // holds decision

				if (mUseAi) {
					await Task.Delay(1500);             // wait for 1.5 seconds... then bang-on
					input = RunAI();                    // hit Ai for next move
				} else input = Console.ReadKey().Key;    // get unbuffered user input.

				action = "\n\tLast action: ";

				switch (input) {
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
					ClearScreen();
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
			Console.WriteLine(mGame.AsString());
		}

		private ConsoleKey RunAI() {

			int options = mLastMoveOk ? 4 : 3;  // 4 or 3 to guess from.
			int pick = new Random().Next(options);
			GameMoves[] moveSelection = { GameMoves.Up, GameMoves.Down, GameMoves.Left, GameMoves.Right };

			short cnt = -1;
			foreach (GameMoves move in moveSelection) {
				if (!mLastMoveOk && mLastMove == move) continue;
				if (++cnt != pick) continue;
				switch (move) {
				case GameMoves.Up: return ConsoleKey.UpArrow;
				case GameMoves.Down: return ConsoleKey.DownArrow;
				case GameMoves.Left: return ConsoleKey.LeftArrow;
				case GameMoves.Right: return ConsoleKey.RightArrow;
				}
				break;
			}
			return ConsoleKey.Q;
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
			mUndoRequestFailed = true;
		}

		public void MoveRequestOutcome(GameMoves move, bool success) {
			mLastMove = move;
			mLastMoveOk = success;
		}
	}

}
