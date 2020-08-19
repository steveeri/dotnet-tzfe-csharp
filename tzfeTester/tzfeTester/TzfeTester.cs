using System;
using System.Threading.Tasks;
using tzfeGameEngine;

namespace tzfeTester {
	class Program {
		static async Task Main(string[] args) {
			Console.WriteLine("Hello We are about to test the tzfeGameEngine");
			GameRunner runner = new GameRunner();
			await runner.PlayAsync();
		}
	}

	// A purpose built task to show how to interact with the GameEngine library.
	class GameRunner : ITzfeGameDelegate {

		// define some strings
		private const string humanPrompt = "\n\t*** Let's PLAY!! ***\n\tActions:[arrows(left,right,up,down), n=new game, q=quit game]: ";
		private const string aiPrompt = "\n\t*** AI IS PLAY!! ***\n\tStandby until game finishes: ";
		private const string nilSlideLeft = " - CAN'T SLIDE OR COMPACT ANY MORE LEFT!";
		private readonly string nilSlideRight = " - CAN'T SLIDE OR COMPACT ANY MORE RIGHT!";
		private readonly string nilSlideUp = " - CAN'T SLIDE OR COMPACT ANY MORE UP!";
		private readonly string nilSlideDown = " - CAN'T SLIDE OR COMPACT ANY MORE DOWN!";
		private readonly string invalidAction = "invalid action. Please try again!";
		private readonly string quitMsg = "\n\n\t*** Thanks for playing :)  Come again! ***\n\n";
		private readonly string youLose = "\n\t*** THE GAME IS LOST. NO MOVES REMAINING. PRESS ENTER TO CONTINUE ***";
		private readonly string youWin = "\n\t*** YOU DA WINNER  !!!AWESOME!!!  PRESS ENTER TO CONTINUE ***";
		private readonly string yourPb = "\n\t*** YOU have hit another PB !!!AWESOME!!! ***\n";
		private readonly string undoFailed = "\n\n\t*** Sorry!! No back moves available!!! ***\n";

		private bool mUseAi = false;
		private int mPrevHiScore = 0;
		private int mSize = -1;
		private TzfeGameEngine mGame;
		private GameMove mLastMove = GameMove.New;
		private DateTime mLastMoveDts = new DateTime();
		private bool mLastMoveOk = true;
		private bool mUndoRequestFailed = false;
		private bool mPlaying = false;

		readonly ConsoleColor savedBackColor = Console.BackgroundColor;
		readonly ConsoleColor savedForeColor = Console.ForegroundColor;

		public GameRunner() { }

		// Function called to supply the outer game loop controlling game.
		// The user can firstly set the game panel size anf whether to play manually or run AI.
		public async Task PlayAsync() {

			this.ClearScreen();

			string answer = null;

			var date = DateTime.Now;
			Console.WriteLine("\n\tRighto!!. Let's play TZFE (2048)\n");

			while (true) {
				bool playSameAsBefore = false;
				if (mSize > 1) {
					Console.Write("\n\tWould you like to replay the same as before, New, or Quit: [Y/n/q]?: ");
					answer = Console.ReadLine() ?? "y";
					answer = answer.Length > 0 ? answer : "y";
					if (answer == "q") return;
					playSameAsBefore = (answer == "y");
				}

				if (!playSameAsBefore) {
					Console.Write("\tWhat size panel do you want?: Default = (4)?: ");
					answer = Console.ReadLine() ?? "4";
					answer = answer.Length > 0 ? answer : "4";
					mSize = Int32.Parse(answer);

					Console.Write("\tWould you like to play yourself or run AI: [AI/self]?: ");
					answer = Console.ReadLine() ?? "a";
					answer = answer.Length > 0 ? answer.ToLower() : "a";
					mUseAi = (answer.Length == 0 || answer == "a" || answer == "ai");
				}

				// Construct new Game.
				mGame = new TzfeGameEngine(this, mSize);
				mPlaying = true;
				mGame.NewGame(mPrevHiScore);

				Console.Write("\n\tOky Doky!!. Running panel size {0}, using AI = {1}.\n\tPress ENTER to continue, else 'Q' to Quit: ", mSize, mUseAi);
				if (Console.ReadKey().Key == ConsoleKey.Q) {
					RestoreScreen();
					return;
				}

				await this.PlayGameAsync(); // play this game.
			}
		}

		// Secondary game loop accepting movement inputs until game ends or is cancelled.
		private async Task PlayGameAsync() {

			ClearScreen();
			var action = "\n";

			var counter = 0;
			var busyStr = ">";

			// Either machine is playing or under manual control.
			while ((mUseAi && mGame.HasMovesRemaining) || (!mUseAi && mPlaying)) {

				if (mUndoRequestFailed) {
					Console.WriteLine(undoFailed);
					mUndoRequestFailed = false;
				}

				ConsoleKey input; // holds decision

				if (mUseAi) {
					// AI User => with size 4 tiles print every 40 moves, size 10 every 100.
					if (counter++ % (mSize*2) == 0) {
						// Clear screen and output updated game board resulting from last input action.
						ClearScreen();
						RenderPanel();
						Console.WriteLine(action);
						busyStr = $"={busyStr}";
						Console.Write($"{aiPrompt}{busyStr}");
					}
					// The bigger the grid the shorter the delay time.
					await Task.Delay(1500 / (mSize * mSize));
					input = RunAI();   // hit Ai for next move
				} else {
					// Human Users provide a natural delay to inputs and rendering.
					ClearScreen();
					RenderPanel();
					Console.WriteLine(action);
					Console.Write(humanPrompt);
					input = Console.ReadKey().Key; // get unbuffered user input.
				}

				action = "\n\tLast action: ";

				switch (input) {
				case ConsoleKey.LeftArrow:
					action += "move left";
					if (!mGame.ActionMove(GameMove.Left)) action += nilSlideLeft;
					break;
				case ConsoleKey.RightArrow:
					action += "move right";
					if (!mGame.ActionMove(GameMove.Right)) action += nilSlideRight;
					break;
				case ConsoleKey.UpArrow:
					action += "move up";
					if (!mGame.ActionMove(GameMove.Up)) action += nilSlideUp;
					break;
				case ConsoleKey.DownArrow:
					action += "move down";
					if (!mGame.ActionMove(GameMove.Down)) action += nilSlideDown;
					break;
				case ConsoleKey.N:
					mGame.NewGame(mPrevHiScore);
					mPlaying = true;
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
					mPlaying = false;
					return;  // Exit the game program here.
				default:
					action += invalidAction;
					break;
				}
			}

			// We ran out of moves. Ensure we render last panel.
			if (mUseAi) { ClearScreen(); RenderPanel(); }
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

		// A calling class to wrap the game Engines AI Move Suggestion capability.
		private ConsoleKey RunAI() {
			switch (this.mGame.AIMoveSuggestion) {
			case GameMove.Up: return ConsoleKey.UpArrow;
			case GameMove.Down: return ConsoleKey.DownArrow;
			case GameMove.Left: return ConsoleKey.LeftArrow;
			case GameMove.Right: return ConsoleKey.RightArrow;
			default: return ConsoleKey.Q;
			}
		}

		// Implemented example use ofTZFE Interface Methods
		// Implemented example use ofTZFE Interface Methods
		public void UpdateTileValue(Transition move) {
			// Can be used to animate all tile movements. Not used in this console app.
		}

		// Recieved signal when user loses game.
		public void UserFail() {
			mPlaying = false;
			ClearScreen();
			RenderPanel();
			Console.WriteLine(youLose);
			Console.Read();
		}

		// Recieved signal when user exceeds Previous Personal Best.
		public void UserPB(int score) {
			Console.WriteLine(yourPb);
		}

		// Recieved signal when user's requested action causes a score change.
		public void UserScoreChanged(int score) {
			if (score > mPrevHiScore) mPrevHiScore = score;
		}

		// Recieved signal when user wins the game (meets or exceeds target).
		public void UserWin() {
			ClearScreen();
			RenderPanel();
			Console.WriteLine(youWin);
			Console.Read();
		}

		// Recieved signal when requsted undo request is unavailable.
		public void UndoRequestFail() {
			mUndoRequestFailed = true;
		}

		// Recieved signal reporting the outome of the last requested action.
		public void MoveRequestOutcome(GameMove move, int moves, bool success, DateTime dts) {
			mLastMove = move;
			mLastMoveOk = success;
			mLastMoveDts = dts;
		}
	}

}
