using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

        public Move Think(Board board, Timer timer)
        {
            bool areWeWhite = board.IsWhiteToMove;
            Move[] allMoves = board.GetLegalMoves();

            // Pick a random move to play if nothing better is found
            Random rng = new();
            Move moveToPlay = allMoves[rng.Next(allMoves.Length)];
            int bestScore = 0;

            foreach (Move move in allMoves)
            {
                board.MakeMove(move);

                if (board.IsInCheckmate())
                {
                    return move;
                }

                int boardScore = MinMax(board, 3, false, int.MinValue, int.MaxValue);

                if (boardScore > bestScore)
                {
                    moveToPlay = move;
                    bestScore = boardScore;
                }

                board.UndoMove(move);
            }

            return moveToPlay;
        }

        int MinMax(Board board, int depth, bool maximizingPlayer, float alpha, float beta)
        {
            if (depth == 0)
            {
                return GetBoardScore(board) * (maximizingPlayer ? 1 : -1);
            }

            if (board.IsInCheckmate())
            {
                return maximizingPlayer ? int.MinValue : int.MaxValue;
            }

            Move[] allMoves = board.GetLegalMoves();

            if (maximizingPlayer)
            {
                int maxEval = int.MinValue;
                foreach (Move move in allMoves)
                {
                    board.MakeMove(move);
                    int eval = MinMax(board, depth - 1, false, alpha, beta);
                    board.UndoMove(move);
                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha)
                    {
                        break;
                    }
                }

                return maxEval;
            }
            else
            {
                int minEval = int.MaxValue;
                foreach (Move move in allMoves)
                {
                    board.MakeMove(move);
                    int eval = MinMax(board, depth - 1, true, alpha, beta);
                    board.UndoMove(move);
                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha)
                    {
                        break;
                    }
                }

                return minEval;
            }
        }

        int GetBoardScore(Board board)
        {
            int boardScore = 0;
            for (int file = 0; file < 8; file++)
            {
                for (int rank = 0; rank < 8; rank++)
                {
                    Square square = new(file, rank);
                    Piece piece = board.GetPiece(square);
                    if (piece != null)
                    {
                        int pieceValue = pieceValues[(int)piece.PieceType];
                        if (piece.IsWhite == board.IsWhiteToMove)
                        {
                            boardScore += pieceValue;
                        }
                        else
                        {
                            boardScore -= pieceValue;
                        }
                    }
                }
            }

            return boardScore;
        }
    }
}