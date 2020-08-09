using System;

namespace tzfeGameEngine {

    public struct TzfeConstants {
        // STD message strings EN
        public const String NO_MORE_MOVES = "Sorry. No more moves possible. Try Undo?";
        public const String NO_MORE_UNDO = "Sorry. No undo's available. Undo's are limited to 5!";
        public const String NEW_HIGH_SCORE = "Oh YES. New PB reached.";
        public const String WINNER = "AWESOME!, you have won the game. Keep playing!!!";

        // STD game specific values
        public const int TILE_CNT = 16;
        public const int DIMENSION = 4;
        public const int WIN_TARGET = 2048;
        public const int EMPTY_TILE_VAL = 0;
        public const int MAX_PREVIOUS_MOVES = 5;
        public const int PB_MESG_THRESHOLD = 100;
        
        // STD random tile selection ratio
        public const int RANDOM_RATIO = 70;  // favours '2's over '4's by ~3.5:1 ~70%
    }

}  // end tzfeGameEngine namespace