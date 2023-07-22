using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 325, 325, 550, 1000, 50000 };

    static int maxDepth = 5;
    Move[] PVArray = new Move[maxDepth];

    static uint PVTableSize = 100000;
    KeyValuePair<ulong, Move>[] PVTable = new KeyValuePair<ulong, Move>[PVTableSize + 2];

    int initPly = 0;

    int[,] MvvLvaScores = new int[13, 13];
    int[,] searchHistory = new int[13, 64];
    Move[,] searchKillers = new Move[2, maxDepth + 1];

    public MyBot()
    {
        InitMvvLva();
    }

    public Move Think(Board board, Timer timer)
    {
        initPly = board.PlyCount;

        ClearForSearch(board);
        for (int depth = 1; depth <= maxDepth; depth++)
            QuiescenceOrAlphaBeta(-99999999, 999999999, depth, board, timer, false);

        return ProbePVTable(board);
    }

    void InitMvvLva()
    {
        for (int attacker = 1; attacker <= 6; attacker++)
            for (int victim = 1; victim <= 6; victim++)
                MvvLvaScores[victim, attacker] = victim * 100 + 6 - attacker;
    }

    void ClearForSearch(Board board)
    {
        for (int i = 0; i < 13; i++)
        {
            for (int j = 0; j < 64; j++)
            {
                searchHistory[i, j] = 0;
                searchKillers[i % 2, j % maxDepth] = Move.NullMove;
            }
        }

        PVTable = new KeyValuePair<ulong, Move>[PVTableSize + 2];
    }

    Move ProbePVTable(Board board)
    {
        uint index = (uint)board.ZobristKey % PVTableSize;
        if (PVTable[index].Key == board.ZobristKey)
            return PVTable[index].Value;

        return Move.NullMove;
    }

    int QuiescenceOrAlphaBeta(int alpha, int beta, int depth, Board board, Timer timer, bool isQuiescence)
    {
        int searchPly = board.PlyCount - initPly;

        if (!isQuiescence && (depth == 0 || timer.MillisecondsElapsedThisTurn >= 1000))
            return QuiescenceOrAlphaBeta(alpha, beta, depth, board, timer, true);

        if (board.IsDraw())
            return 0;

        if (isQuiescence)
        {
            int boardScore = GetBoardScore(board);

            if (searchPly > maxDepth)
                return boardScore;

            if (boardScore >= beta)
                return beta;

            if (boardScore > alpha)
                alpha = boardScore;
        }

        Move[] moves = board.GetLegalMoves(isQuiescence);
        int oldAlpha = alpha;
        Move bestMove = Move.NullMove;
        Move PVMove = ProbePVTable(board);

        var moveListScores = new KeyValuePair<Move, int>[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            int score = searchHistory[(int)move.MovePieceType, move.TargetSquare.Index];
            if (move.IsCapture)
                score = MvvLvaScores[(int)move.CapturePieceType, (int)move.MovePieceType] + 1000000;
            else if (searchKillers[0, searchPly] == move)
                score = 900000;
            else if (searchKillers[1, searchPly] == move)
                score = 800000;

            moveListScores[i] = new(move, score);
        }

        if (!isQuiescence)
        {
            if (PVMove != Move.NullMove)
            {
                for (int i = 0; i < moveListScores.Length; i++)
                {
                    if (moveListScores[i].Key == PVMove)
                    {
                        moveListScores[i] = new(PVMove, 2000000);
                        break;
                    }
                }
            }

            if (moves.Length == 0)
                return board.IsInCheck() ? -100000 - depth : 0;
        }

        for (int i = 0; i < moveListScores.Length; i++)
        {
            // ---------- PickNextMove ---------- //
            int bestScore = 0;
            int bestIndex = 0;

            for (int j = i; j < moveListScores.Length; j++)
            {
                if (moveListScores[j].Value > bestScore)
                {
                    bestScore = moveListScores[j].Value;
                    bestIndex = j;
                }
            }

            var temp = moveListScores[i];
            moveListScores[i] = moveListScores[bestIndex];
            moveListScores[bestIndex] = temp;
            // ---------- PickNextMove ---------- //

            Move move = moveListScores[i].Key;

            board.MakeMove(move);
            int score = -QuiescenceOrAlphaBeta(-beta, -alpha, depth - 1, board, timer, isQuiescence);
            board.UndoMove(move);

            if (score > alpha)
            {
                if (score >= beta)
                {
                    if (!isQuiescence && !move.IsCapture)
                    {
                        searchKillers[1, searchPly] = searchKillers[0, searchPly];
                        searchKillers[0, searchPly] = move;
                    }

                    return beta;
                }
                alpha = score;
                bestMove = move;

                if (!isQuiescence && !move.IsCapture)
                {
                    searchHistory[(int)move.MovePieceType, move.TargetSquare.Index] += depth;
                }
            }
        }

        if (alpha != oldAlpha)
        {
            uint index = (uint)board.ZobristKey % PVTableSize;
            PVTable[index] = new KeyValuePair<ulong, Move>(board.ZobristKey, bestMove);
        }

        return alpha;
    }

    int GetBoardScore(Board board)
    {
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
            return distanceFromBackrank == 7 ? 0 : -70;
        else if (piece.IsQueen)
            return 0;
        else if (piece.IsRook)
            return distanceFromBackrank == 1 ? 25 : Math.Max(0, 10 - distanceFromCenter * 5);
        else if (piece.IsBishop || piece.IsKnight)
            return Math.Max(0, 20 - distanceFromCenter * 5);
        else if (piece.IsPawn)
        {
            int pawnDistance = distanceFromBackrank + distanceFromCenterX;
            return distanceFromBackrank <= 5 ? Math.Max(0, 20 - pawnDistance * 5) : 0;
        }

        return 0;
    }
}
