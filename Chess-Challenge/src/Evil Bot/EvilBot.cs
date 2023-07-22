using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        int[] pieceValues = { 0, 100, 325, 325, 550, 1000, 50000 };
        int[] victimScore = { 0, 100, 200, 300, 400, 500, 600 };

        static int maxDepth = 5;
        static uint PVTableSize = 100000;
        KeyValuePair<ulong, Move>[] PVTable = new KeyValuePair<ulong, Move>[PVTableSize + 2];
        Move[] PVArray = new Move[maxDepth];

        int[,] searchHistory = new int[13, 64];
        int[,] searchKillers = new int[2, maxDepth];
        int searchPly = 0;
        int searchNodes = 0;

        int[,] MvvLvaScores = new int[13, 13];

        List<ulong> history = new();

        public EvilBot()
        {
            InitMvvLva();
        }

        public Move Think(Board board, Timer timer)
        {
            try
            {
                history.Add(board.ZobristKey);
                Move bestMove = SearchPosition(board, timer);

                board.MakeMove(bestMove);
                history.Add(board.ZobristKey);

                return bestMove;
            }
            catch (Exception e)
            {
                Move[] moves = board.GetLegalMoves();
                return moves[0];
            }
        }

        void InitMvvLva()
        {
            for (int attacker = (int)PieceType.Pawn; attacker <= (int)PieceType.King; attacker++)
                for (int victim = (int)PieceType.Pawn; victim <= (int)PieceType.King; victim++)
                    MvvLvaScores[victim, attacker] = victimScore[victim] + 6 - (victimScore[attacker] / 100);
        }

        void PickNextMove(int moveNum, ref KeyValuePair<Move, int>[] moveListScores)
        {
            int bestScore = 0;
            int bestIndex = 0;

            for (int i = moveNum; i < moveListScores.Length; i++)
            {
                if (moveListScores[i].Value > bestScore)
                {
                    bestScore = moveListScores[i].Value;
                    bestIndex = i;
                }
            }

            KeyValuePair<Move, int> temp = moveListScores[moveNum];
            moveListScores[moveNum] = moveListScores[bestIndex];
            moveListScores[bestIndex] = temp;
        }

        bool IsRepetition(Board board)
        {
            for (int i = 0; i < history.Count - 1; i++)
            {
                if (history[i] == board.ZobristKey)
                {
                    return true;
                }
            }

            return false;
        }

        void ClearForSearch(Board board)
        {
            for (int i = 0; i < 13; i++)
            {
                for (int j = 0; j < 64; j++)
                {
                    searchHistory[i, j] = 0;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < maxDepth; j++)
                {
                    searchKillers[i, j] = 0;
                }
            }

            PVTable = new KeyValuePair<ulong, Move>[PVTableSize + 2];
            searchPly = 0;
            searchNodes = 0;
        }

        void StorePVMove(Board board, Move move)
        {
            uint index = (uint)board.ZobristKey % PVTableSize;
            PVTable[index] = new KeyValuePair<ulong, Move>(board.ZobristKey, move);
        }

        Move ProbePVTable(Board board)
        {
            uint index = (uint)board.ZobristKey % PVTableSize;
            if (PVTable[index].Key == board.ZobristKey)
            {
                return PVTable[index].Value;
            }

            return Move.NullMove;
        }

        int GetPVLine(Board board, int depth)
        {
            Move move = ProbePVTable(board);
            List<Move> movesToUndo = new();
            int count = 0;

            while (move != Move.NullMove && count < depth)
            {
                if (board.GetLegalMoves().Contains(move))
                {
                    movesToUndo.Insert(0, move);
                    board.MakeMove(move);
                    PVArray[count++] = move;
                }
                else
                {
                    break;
                }

                move = ProbePVTable(board);
            }

            foreach (Move moveToUndo in movesToUndo)
            {
                board.UndoMove(moveToUndo);
            }

            return count;
        }

        Move SearchPosition(Board board, Timer timer)
        {
            Move bestMove = Move.NullMove;
            int bestScore = int.MinValue;
            ClearForSearch(board);

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                bestScore = AlphaBeta(-9999999, 99999999, depth, board, timer);
                int pvMoves = GetPVLine(board, depth);
                bestMove = PVArray[0];
            }

            return bestMove;
        }

        int AlphaBeta(int alpha, int beta, int depth, Board board, Timer timer)
        {
            searchNodes++;

            if (depth == 0 || timer.MillisecondsElapsedThisTurn >= 1000)
                return GetBoardScore(board);

            Move[] moves = board.GetLegalMoves();
            int oldAlpha = alpha;
            Move bestMove = Move.NullMove;

            KeyValuePair<Move, int>[] moveListScores = new KeyValuePair<Move, int>[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];
                int score = move.IsCapture ? MvvLvaScores[(int)move.CapturePieceType, (int)move.MovePieceType] : 0;
                moveListScores[i] = new KeyValuePair<Move, int>(move, score);
            }

            if (moves.Length == 0)
            {
                return board.IsInCheck() ? -100000 - depth : 0;
            }

            for (int i = 0; i < moveListScores.Length; i++)
            {
                PickNextMove(i, ref moveListScores);
                Move move = moveListScores[i].Key;

                board.MakeMove(move);
                int score = -AlphaBeta(-beta, -alpha, depth - 1, board, timer);
                board.UndoMove(move);

                if (score > alpha)
                {
                    if (score >= beta)
                    {
                        return beta;
                    }
                    alpha = score;
                    bestMove = move;
                }
            }

            if (alpha != oldAlpha)
            {
                StorePVMove(board, bestMove);
            }

            return alpha;
        }

        int GetBoardScore(Board board)
        {
            if (board.IsDraw())
                return board.IsWhiteToMove ? int.MinValue : int.MaxValue;

            int score = 0;
            for (int file = 0; file < 8; file++)
            {
                for (int rank = 0; rank < 8; rank++)
                {
                    Square square = new(file, rank);
                    Piece piece = board.GetPiece(square);
                    if (piece != null)
                    {
                        int pieceValue = pieceValues[(int)piece.PieceType] + PieceEvaluation(piece, square);
                        score += pieceValue * (piece.IsWhite ? 1 : -1);
                    }
                }
            }

            return score * (board.IsWhiteToMove ? 1 : -1);
        }

        int PieceEvaluation(Piece piece, Square square)
        {
            int distanceFromCenterX = (int)Math.Abs(square.File - 3.5);
            int distanceFromCenterY = (int)Math.Abs(square.Rank - 3.5);
            int distanceFromBackrank = piece.IsWhite ? square.Rank : 7 - square.Rank;
            int distanceFromCenter = distanceFromCenterX + distanceFromCenterY;

            if (piece.IsKing)
            {
                return distanceFromBackrank == 7 ? 0 : -70;
            }
            else if (piece.IsQueen)
            {
                return 0;
            }
            else if (piece.IsRook)
            {
                return distanceFromBackrank == 1 ? 25 : Math.Max(0, 10 - distanceFromCenter * 5);
            }
            else if (piece.IsBishop || piece.IsKnight)
            {
                return Math.Max(0, 20 - distanceFromCenter * 5);
            }
            else if (piece.IsPawn)
            {
                int pawnDistance = distanceFromBackrank + distanceFromCenterX;
                return distanceFromBackrank <= 5 ? Math.Max(0, 20 - pawnDistance * 5) : 0;
            }

            return 0;
        }
    }
}