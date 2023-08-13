﻿using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class EvilBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
    const int MAX_DEPHT = 5; // You can adjust this value depending on how much time you have.

    private int evaluatedPositions = 0;
    public Move Think(Board board, Timer timer)
    {
        
        //weiß ist maximising, schwarz ist minimizing spieler
        var (bestMove, eval) = AlphaBeta(board, MAX_DEPHT, int.MinValue, int.MaxValue, board.IsWhiteToMove);
        //Console.WriteLine("The best move ist: " + bestMove.StartSquare +  " -> " + bestMove.TargetSquare);
        Console.WriteLine("Evil: " + evaluatedPositions + " Positions. Best value: " + eval);
        return bestMove;
    }

    public (Move,int) AlphaBeta(Board board, int depth, int alpha, int beta, bool maximizingPlayer)
    {
        Move[] legalMoves = board.GetLegalMoves();
        if (depth == 0 || board.GetLegalMoves().Length == 0)
        {
            return (Move.NullMove, evaluate(board, depth));
        }

        Move bestMove = Move.NullMove;

        if (maximizingPlayer)
        {
            int maxEval = int.MinValue;
            foreach (var move in legalMoves)
            {
                board.MakeMove(move);

                var (childMove, eval) = AlphaBeta(board, depth - 1, alpha, beta, false);
                if (eval > maxEval)
                {
                    maxEval = eval;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, eval);

                board.UndoMove(move);

                if (beta <= alpha)
                {
                    break;
                }

               
            }
            return (bestMove, maxEval);
        }
        else
        {
            legalMoves = board.GetLegalMoves();
            int minEval = int.MaxValue;
            foreach (var move in legalMoves)
            {
               board.MakeMove(move);

               var (childMove, eval) = AlphaBeta(board, depth - 1, alpha, beta, true);
               if (eval < minEval)
               {
                   minEval = eval;
                   bestMove = move;
               }

               beta = Math.Min(beta, eval);
               board.UndoMove(move);

               if (beta <= alpha)
               {
                   break;
               }
            }

            return (bestMove, minEval);
        }
    }

    public int evaluate(Board board, int remainingDepth)
    {
        ulong zobrisKey = board.ZobristKey;

        evaluatedPositions += 1;

            if (board.IsInCheckmate())
            {
                if (board.IsWhiteToMove)
                {
                    return int.MinValue;
                }

                return int.MaxValue;
            }

            int whiteScore = 0;
            int blackScore = 0;
            PieceList[] allPieces = board.GetAllPieceLists();
            foreach (var pieceList in allPieces)
            {
                if (pieceList.Count > 0)
                {
                    if (pieceList[0].IsWhite)
                    {
                        whiteScore += pieceValues[(int)pieceList[0].PieceType] * (pieceList.Count - 1);
                    }
                    else
                    {
                        blackScore += pieceValues[(int)pieceList[0].PieceType] * (pieceList.Count - 1);
                    }
                }
            }

            return whiteScore - blackScore;
    }
}
