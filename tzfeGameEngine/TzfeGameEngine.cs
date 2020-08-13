using System;
using System.Collections.Generic;
using System.Linq;

namespace tzfeGameEngine {

	// Required protocol that the instantiating delegate class must implement to use the game engine.
	public interface ITzfeGameDelegate {
		void UpdateTileValue(Transition move);
		void UserPB(int score);
		void UserWin();
		void UserFail();
		void UndoRequestFail();
		void UserScoreChanged(int score);
		void MoveRequestOutcome(GameMoves move, int moves, bool success, DateTime dts);
	}

	// Allowable game moves actions. Caller passes these to ActionMove() function.
	public enum GameMoves { Up, Down, Left, Right, New };

	// Allowable tile movement sequences visible externally within Transition records.
	public enum TileMoveType { Add, Slide, Merge, Clear, Reset };


	// The core of the 2048 game logic. Game board moves are tracked via the compilation of 
	// transitions involved in a tile movement.  The game engine supports dynamic sizing at 
	// time of construction, and suports user defined win and undo capabilities.  The engine 
	// uses the above enumerations and Interface to allow caller to interact with it. 
	public class TzfeGameEngine {

		private ITzfeGameDelegate mGameDelegate;

		private readonly int mGridCount;
		private readonly int mDimension;
		private readonly int mBlankTile;
		private readonly int mRandomRatio;
		private readonly int mWinTarget;
		private readonly int mMaxUndos;

		private DateTime? mStartDts;
		private int mScore;
		private int mMoves;
		private int mPreviousHighScore;
		private int mMaxTile;
		private int mNumEmpty;

		private readonly List<GameBoardRecord> mPreviousMoves = new List<GameBoardRecord>();
		private readonly List<Transition> mTransitions = new List<Transition>();
		private readonly List<int> mTiles = new List<int>();

		private Random mRand = new Random();

		// Primary constructor.
		public TzfeGameEngine(
			ITzfeGameDelegate gameDelegate,
			int dimension = 4,
			int randomRatio = 70,
			int winTarget = 2048,
			int maxUndos = 5) {

			// Check that we are receiving valid workable values
			mGameDelegate = gameDelegate ?? throw new ArgumentException("game delegate must not be null");
			if (dimension < 2) throw new ArgumentException("Size of board must be greater than 1");
			if (dimension > 100) throw new ArgumentException("Size of board must not be rediculously large");

			mGridCount = dimension * dimension;
			mNumEmpty = mGridCount;
			mDimension = dimension;
			mRandomRatio = randomRatio;
			mWinTarget = winTarget;
			mMaxUndos = maxUndos;
			mPreviousHighScore = 0;
			mBlankTile = 0;
			mMaxTile = 0;
			mScore = 0;
			mMoves = 0;
		}

		// Reset the playing board, and generate rendering transition records
		public void NewGame(int newHighScore = 0) {
			mPreviousHighScore = newHighScore;
			mNumEmpty = mGridCount;
			mMaxTile = mBlankTile;
			mScore = 0;
			mMoves = 1;
			mStartDts = DateTime.Now;

			// Clear all lists/arrays.
			mPreviousMoves.Clear();
			mTransitions.Clear();
			mTiles.Clear();

			// Create clean array of tiles, and associated transitions
			for (int i = 0; i < mGridCount; i++) {
				mTiles.Add(+mBlankTile);
				mTransitions.Add(new Transition(TileMoveType.Clear, mBlankTile, i));
			}
			mTiles.TrimExcess();  // trim to actual size;

			AddNewTile(2);
			AddNewTile(2);
			mPreviousMoves.Insert(0, GetGameBoardRecord(GameMoves.New, true));
			ApplyGameMoves();
			mGameDelegate.MoveRequestOutcome(GameMoves.New, mMoves, true, new DateTime());
		}

		// RePlot the game board to an earlier time.
		private void ReplotBoard() {
			mTransitions.Clear();
			for (int i = 0; i < mGridCount; i++) {
				mTransitions.Add(new Transition(TileMoveType.Reset, mTiles[i], i));
			}
			ApplyGameMoves();
		}

		// Create and return a current game board status record object.
		private GameBoardRecord GetGameBoardRecord(GameMoves move, bool success) {
			return new GameBoardRecord(mTiles, mScore, mNumEmpty, mMaxTile, move, success);
		}

		// Returns the date time stamp the current game commenced.
		public DateTime? NewGameStartDts {
			get { return mStartDts?.Date; }
		}

		// Returns the number of moves relative to the current play.
		public int NumMoves {
			get { return mMoves; }
		}

		// Returns true if game has met or exceed win target.
		public bool AcheivedTarget {
			get { return (mMaxTile >= mWinTarget); }
		}

		// Gets the int value at position in the linear array.
		public int? GetTileValue(int at) {
			if (at >= 0 && at < mGridCount) return mTiles[at];
			return null;
		}

		// Gets the int value at (X,Y) position coordinates.
		// Zero-Zero is in position top left... down, then top next column... down.
		// Typ.   | 0,0 | 0,1 | 0,2 |    Eample is (3 x 3 grid) = 9 tiles.
		//        | 1,0 | 1,1 | 1,2 |
		//        | 2,0 | 2,1 | 2,2 |
		public int? GetTileValue(int posX, int posY) {

			if (posX < 0 || posX >= mDimension) return null;
			if (posY < 0 || posY >= mDimension) return null;

			return mTiles[(posY * mDimension) + posX];
		}

		// If a vacant position exists then use seedValue to place a new tile in a random location.
		private bool AddNewTile(int seedValue = -1) {
			if (mNumEmpty == 0) return false;

			int value = seedValue;
			if (seedValue < 2 || seedValue % 2 != 0 || seedValue > 4) {
				// Randomly select 2 or 4 at a ratio 3.5:1 in favour ot 2.
				int sample = mRand.Next(100);
				if (sample >= mRandomRatio) {
					value = 4;
				} else {
					value = 2;
				}
			}

			int pos = mRand.Next(mNumEmpty);
			int blanksFound = 0;

			for (int i = 0; i < mGridCount; i++) {
				if (mTiles[i] == mBlankTile) {
					if (blanksFound == pos) {
						mTiles[i] = value;
						if (value > mMaxTile) mMaxTile = value;
						mNumEmpty -= 1;
						mTransitions.Add(new Transition(TileMoveType.Add, value, i));
						return true;
					}
					blanksFound += 1;
				}
			}
			return false;
		}

		// Check up-down for compact moves remaining. 
		// Returns tuple of count and scale of compaction.
		public (int cnt, int factor) CompactVerticallyHint {
			get {
				int count = 0;
				int factor = 0;
				int arrLimitY = mGridCount - 1;
				for (int i = 0; i < arrLimitY; i++) {
					if ((i + 1) % mDimension > 0) {
						// The bigger the adjacent tiles... the bigger the hint.
						if (mTiles[i] > 0 && mTiles[i] == mTiles[i + 1]) {
							count++;
							factor += mTiles[i] * mTiles[i];
						}
					}
				}
				return (count, factor);
			}
		}

		// check left-right for compact moves remaining.
		// Returns tuple of count and scale of compaction.
		public (int cnt, int factor) CompactHorizontallyHint {
			get {
				int count = 0;
				int factor = 0;
				int arrLimitX = mGridCount - mDimension;
				for (int i = 0; i < arrLimitX; i++) {
					// The bigger the adjacent tiles... the bigger the hint.
					if (mTiles[i] > 0 && mTiles[i] == mTiles[i + mDimension]) {
						count++;
						factor += mTiles[i] * mTiles[i];
					}
				}
				return (count, factor);
			}
		}

		// Determine if any move conditions remain.
		public bool HasMovesRemaining() {
			if (mNumEmpty > 0) return true;
			if (CompactVerticallyHint.cnt > 0) return true;
			if (CompactHorizontallyHint.cnt > 0) return true;
			return false;
		}

		// THIS FUNCTION IS THE MAIN CONTROLLER FOR GAME MOVES
		// THIS FUNCTION IS THE MAIN CONTROLLER FOR GAME MOVES
		public bool ActionMove(GameMoves move) {

			var tempScore = this.mScore;

			// Clean the transition record as we are doing a new move action.
			mTransitions.Clear();

			bool changed = false;

			switch (move) {
			case GameMoves.Up:
				changed = ActionMoveUp();
				break;
			case GameMoves.Down:
				changed = ActionMoveDown();
				break;
			case GameMoves.Left:
				changed = ActionMoveLeft();
				break;
			case GameMoves.Right:
				changed = ActionMoveRight();
				break;
			}

			// Board updated => add new tile and store previous changes game board
			if (changed) {
				mMoves++;
				AddNewTile();
				mPreviousMoves.Insert(0, GetGameBoardRecord(move, true));
				// If over max allowed undo moves then delete oldest held record.
				if (mPreviousMoves.Count() > mMaxUndos + 1) {
					mPreviousMoves.RemoveAt(mPreviousMoves.Count() - 1);
				}
				ApplyGameMoves();
			}

			// Report back to delegate what happened with requested action
			mGameDelegate.MoveRequestOutcome(move, mMoves, changed, new DateTime());

			// index zero => current/last board result
			// Check to see if score was updated.
			if (mScore != tempScore) {
				mGameDelegate.UserScoreChanged(mScore);
			}

			// Check to see if run out of moves => Report state to delegate.
			if (!HasMovesRemaining()) {
				if (mMaxTile >= mWinTarget) {
					mGameDelegate.UserWin();
				} else {
					mGameDelegate.UserFail();
				}
				if (mScore != tempScore && mScore > mPreviousHighScore) {
					mGameDelegate.UserPB(mScore);
				}
				return false;
			}

			return changed;
		}

		// Callback to the delegate to report of game board changes in terms of tile transitions.
		// The application of the sum of transitions would allow caller to animate tile movements.
		private void ApplyGameMoves() {
			foreach (Transition trans in mTransitions) {
				mGameDelegate.UpdateTileValue(trans);
			}
		}

		// User selected action to regress state and move sequences back to previous play point.
		public bool GoBackOneMove() {
			if (mPreviousMoves.Count() > 1) {
				GameBoardRecord pm = mPreviousMoves[1];
				mTiles.Clear(); // first clear transactions.
				foreach (int val in pm.mTiles) mTiles.Add(val);
				mScore = pm.mScore;
				mMoves--;
				mNumEmpty = pm.mNumEmpty;
				mMaxTile = pm.mMaxTile;
				mPreviousMoves.RemoveAt(0);
				ReplotBoard(); // redraw and create transitions for re-rendering the board.
				mGameDelegate.MoveRequestOutcome(pm.mMove, mMoves, pm.mMoveSuccess, new DateTime());
				return true;
			}
			mGameDelegate.UndoRequestFail();
			return false;
		}

		// Does equivalent of Slide/Compact...(0,4,8,12), then Slide...(1,5,9,13) ...;
		private bool ActionMoveLeft() {
			var result = false;
			int[] arr = new int[mDimension]; // reused
			for (int i = 0; i < mDimension; i++) {
				for (int j = 0; j < mDimension; j++) arr[j] = i + j * mDimension;
				result = SlideTileRowOrColumn(arr) || result;
				result = CompactTileRowOrColumn(arr) || result;
				result = SlideTileRowOrColumn(arr) || result;
			}
			return result;
		}

		// Does equivalent of Slide/Compact...(12,8,4,0), then Slide...(13,9,5,1) ...;
		private bool ActionMoveRight() {
			var result = false;
			int[] arr = new int[mDimension]; // reused
			for (int i = mGridCount - mDimension; i < mGridCount; i++) {
				for (int j = 0; j < mDimension; j++) arr[j] = i - j * mDimension;
				result = SlideTileRowOrColumn(arr) || result;
				result = CompactTileRowOrColumn(arr) || result;
				result = SlideTileRowOrColumn(arr) || result;
			}
			return result;
		}

		// Does equivalent of Slide/Compact...(0,1,2,3), then Slide...(4,5,6,7) ...;
		private bool ActionMoveUp() {
			var result = false;
			int[] arr = new int[mDimension]; // reused
			for (int i = 0; i < mDimension; i++) {
				int startIdx = i * mDimension;
				for (int j = 0; j < mDimension; j++) arr[j] = startIdx + j;
				result = SlideTileRowOrColumn(arr) || result;
				result = CompactTileRowOrColumn(arr) || result;
				result = SlideTileRowOrColumn(arr) || result;
			}
			return result;
		}

		// Does equivalent of Slide/Compact...(3,2,1,0), then Slide...(7,6,5,4) ...;
		private bool ActionMoveDown() {
			var result = false;
			int[] arr = new int[mDimension]; // reused
			for (int i = 0; i < mDimension; i++) {
				int startIdx = ((i + 1) * mDimension) - 1;
				for (int j = 0; j < mDimension; j++) arr[j] = startIdx - j;
				result = SlideTileRowOrColumn(arr) || result;
				result = CompactTileRowOrColumn(arr) || result;
				result = SlideTileRowOrColumn(arr) || result;
			}
			return result;
		}

		// Slide all tiles in required direction if empty spaces exist.
		private bool SlideTileRowOrColumn(params int[] indexes) {
			bool moved = false;

			// Do we have some sliding to do, or not?
			int es = 0;  // empty spot index
			for (int i = 0; i < indexes.Count(); i++) {
				if (mTiles[indexes[es]] != mBlankTile) {
					es++;
					continue;
				}
				if (mTiles[indexes[i]] == mBlankTile) {
					continue;
				}
				// Otherwise we have a slide condition
				mTiles[indexes[es]] = mTiles[indexes[i]];
				mTiles[indexes[i]] = mBlankTile;
				mTransitions.Add(new Transition(TileMoveType.Slide, mTiles[indexes[es]], indexes[es], indexes[i]));
				moved = true;
				es++;
			}
			return moved;
		}

		// Compact adjacent tile in required direction if they have the same value. 
		private bool CompactTileRowOrColumn(params int[] indexes) {

			bool compacted = false;

			for (int i = 0; i < (indexes.Count() - 1); i++) {
				if (mTiles[indexes[i]] != mBlankTile && mTiles[indexes[i]] == mTiles[indexes[i + 1]]) { // we found a matching pair
					int ctv = mTiles[indexes[i]] * 2;  // = compacted tile value
					mTiles[indexes[i]] = ctv;
					mTiles[indexes[i + 1]] = mBlankTile;
					mScore += ctv;
					if (ctv > mMaxTile) {
						mMaxTile = ctv;
					}  // is this the biggest tile # so far
					mTransitions.Add(new Transition(TileMoveType.Merge, ctv, indexes[i], indexes[i + 1]));
					compacted = true;
					mNumEmpty++;
				}
			}
			return compacted;
		}

		// Zero is in position top left... down, then top next column... down.
		// E.g.   | 0 | 3 | 6 |    (3 x 3 grid) = 9 tiles.
		//        | 1 | 4 | 7 |
		//        | 2 | 5 | 8 |
		public String AsString() {

			var nowTicks = DateTime.Now.Ticks;
			DateTime time = new DateTime(nowTicks - mStartDts?.Ticks ?? nowTicks);

			string bar = "-";
			for (int i = 0; i < mDimension; i++) bar += "-----";

			String lines = "\n          [[[ 2048 ]]]\n";
			lines += $"\n           Score: {mScore}";
			lines += $"\n        Hi-Score: {mPreviousHighScore}";
			lines += $"\n        Max Tile: {mMaxTile}";
			lines += $"\n           Moves: {mMoves}";
			lines += $"\n            Time: {time:mm:ss}";
			lines += $"\n       {bar}\n";

			// Row by row from the top down, capture values going left to right.
			for (int i = 0; i < mDimension; i++) {
				String line = "       ";
				for (int j = 0; j < mDimension; j++) {
					line += $"|{mTiles[i + j * mDimension],4}";
				}
				lines += line + $"|\n       {bar}\n";
			}
			lines += $"       {bar}\n";

			return lines;
		}

		// Returns a simple linear string representation of the game board primarily
		// for debugging purposes for gameEngine users.
		public String AsStringLinear() {
			string line = "|";
			for (int i = 0; i < mGridCount; i++) line += mTiles[i] + "|";
			return line;
		}

	} // end class.

	// Tile movement instruction records for the game board renderer to animate or action.
	public class Transition {
		readonly TileMoveType action;
		readonly int value;
		readonly int location;
		readonly int oldLocation;

		public Transition(TileMoveType action, int value, int location, int oldLocation = -1) {
			this.action = action;
			this.value = value;
			this.location = location;
			this.oldLocation = oldLocation;
		}
	} // end class.

	// Snapshot Object containing the status of previous game moves. Support Undo capability.
	public class GameBoardRecord {
		public readonly List<int> mTiles = new List<int>();
		public readonly int mScore;
		public int mNumEmpty;
		public readonly int mMaxTile;
		public readonly GameMoves mMove;
		public readonly bool mMoveSuccess;

		public GameBoardRecord(
			List<int> tiles, int score, int numEmpty, 
			int maxTile, GameMoves move, bool moveSuccess) {

			foreach (int val in tiles) mTiles.Add(val);
			mTiles.TrimExcess();
			mScore = score;
			mNumEmpty = numEmpty;
			mMaxTile = maxTile;
			mMove = move;
			mMoveSuccess = moveSuccess;
		}
	} // end class.

} // end tzfeGameEngine namspace 
