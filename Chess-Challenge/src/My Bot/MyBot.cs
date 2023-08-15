using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

//todo: bot stürzt ab wenn die zeit abläuft und die max tiefe noch nicht erreicht wurde
public class MyBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
    const int MAX_DEPHT = 10; // You can adjust this value depending on how much time you have.
    private bool isWhite;
    private int timeLimit = 10000;
    private Move lastBestMove = Move.NullMove;

    private Dictionary<ulong, TranspositionEntry> transpositionTable = new();

    private int evaluatedPositions;
    
    public Move Think(Board board, Timer timer)
    {
        //weiß ist maximising, schwarz ist minimizing spieler
        //var (bestMove, eval) = AlphaBeta(board, MAX_DEPHT, int.MinValue, int.MaxValue, isWhite = board.IsWhiteToMove);
        timeLimit = (int)Math.Round(timer.IncrementMilliseconds + timer.MillisecondsRemaining * 0.1);
        Console.WriteLine("Time Limit "+timeLimit);
        var bestMove = IterativeDeepening(board,timer);
        Console.WriteLine("Evaluated " + evaluatedPositions + " Positions. Best value: ");
        lastBestMove = bestMove;

        Console.WriteLine("Best move is: " + bestMove);
        
        return bestMove;
    }

    public (Move,int) AlphaBeta(Board board, int depth, int alpha, int beta, bool maximizingPlayer, Timer timer)
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

                if (timer.MillisecondsElapsedThisTurn >= timeLimit)
                {
                    return (bestMove, maxEval);
                }
                var (childMove, eval) = AlphaBeta(board, depth - 1, alpha, beta, false,timer);
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

               if (timer.MillisecondsElapsedThisTurn >= timeLimit)
               {
                   return (bestMove, minEval);
               }
               var (childMove, eval) = AlphaBeta(board, depth - 1, alpha, beta, true,timer);
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

    private Move IterativeDeepening(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        for (int depht = 1; depht < MAX_DEPHT; depht++)
        {
            if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            {
                Console.WriteLine("Best move is: " + bestMove);
                break;
            }

            var (move, _) = AlphaBeta(board, depht, int.MinValue, int.MaxValue, isWhite,timer);
            bestMove = move;
        }

        return bestMove;
    }

    private int evaluate(Board board, int remainingDepth)
    {
        var zobrisKey = board.ZobristKey;
        
        if (transpositionTable.ContainsKey(zobrisKey) && transpositionTable[zobrisKey].Depht >= remainingDepth)
        {
            return transpositionTable[zobrisKey].Value;
        }
        

        evaluatedPositions += 1;
        //Todo: das funktioniert nicht - spiel endet immer in unentschieden the fuck
        if (board.IsDraw()) return 0;
        
        if (board.IsInCheckmate())
        {
            if (isWhite == board.IsWhiteToMove)
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
        var moveList = moves.OrderByDescending(move =>
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
        //Prepend the last best move to the array
        return moveList;
        //todo: funktioniert glaube ich noch nicht richtig
        if (lastBestMove != Move.NullMove && moveList.Contains(lastBestMove))
        {
            Move[] newOrderedMoveList = new Move[moveList.Length+1];
            newOrderedMoveList[0] = lastBestMove;
            Array.Copy(moveList,0,newOrderedMoveList,1,moveList.Length);
            return newOrderedMoveList;
        }

        
    }

}

public class TranspositionEntry
{
    public int Value { get; set; }
    public int Depht { get; set; }
}