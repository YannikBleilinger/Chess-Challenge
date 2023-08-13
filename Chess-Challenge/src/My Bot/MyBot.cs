using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class MyBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
    const int MAX_DEPHT = 5; // You can adjust this value depending on how much time you have.

    private Dictionary<ulong, TranspositionEntry> transpositionTable = new();

    private int evaluatedPositions;
    public Move Think(Board board, Timer timer)
    {
        
        //weiß ist maximising, schwarz ist minimizing spieler
        var (bestMove, eval) = AlphaBeta(board, MAX_DEPHT, int.MinValue, int.MaxValue, board.IsWhiteToMove);
        Console.WriteLine("Evaluated " + evaluatedPositions + " Positions. Best value: " + eval);
        return bestMove;
    }

    public (Move,int) AlphaBeta(Board board, int depth, int alpha, int beta, bool maximizingPlayer)
    {
        Move[] legalMoves = board.GetLegalMoves();
        legalMoves = OrderMoves(legalMoves, board);
        
        if (depth == 0 || legalMoves.Length == 0)
        {
            return (Move.NullMove, evaluate(board, depth));
        }

        Move bestMove = Move.NullMove;
        //Fallunterscheidung je nach Spieler
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
        
        if (transpositionTable.ContainsKey(zobrisKey) && transpositionTable[zobrisKey].Depht >= remainingDepth)
        {
            return transpositionTable[zobrisKey].Value;
        }
        

        evaluatedPositions += 1;

        if (board.IsInCheckmate() || board.IsDraw())
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
                    whiteScore += pieceValues[(int)pieceList[0].PieceType] * (pieceList.Count);
                }
                else
                {
                    blackScore += pieceValues[(int)pieceList[0].PieceType] * (pieceList.Count);
                }
            }
        }

        transpositionTable[zobrisKey] = new TranspositionEntry {Value = whiteScore-blackScore,Depht = remainingDepth};
        return whiteScore - blackScore;
    }

    private Move[] OrderMoves(Move[] moves, Board board)
    {
        return moves.OrderByDescending(move =>
        {
            board.MakeMove(move);
            int moveValue = -1000;

            if (board.IsInCheck())
            {
                moveValue = 10000;
            } else if (move.IsCapture)
            {
               moveValue = pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] 
                                        - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType];
            }
            board.UndoMove(move);
            return moveValue;
        }).ToArray();
    }
}

public class TranspositionEntry
{
    public int Value { get; set; }
    public int Depht { get; set; }
}