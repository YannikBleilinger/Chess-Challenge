using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

//todo: bot macht illegale züge (vorallem im Schach)
//todo: implement quiescence Search
public class EvilBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 320, 330, 500, 900, 20000 };
    const int MAX_DEPHT = 20; // You can adjust this value depending on how much time you have.
    private const int MAX_ENTRIES = 13000000;
    private bool isWhite;
    private int timeLimit = 10000;
    private Move lastBestMove = Move.NullMove;

    private Dictionary<ulong, TranspositionEntry> transpositionTable = new();

    //Controll variables
    private int evaluatedPositions;
    private int transposCutoffs;
    private int reachedDepth;
    
    public Move Think(Board board, Timer timer)
    {
        //weiß ist maximising, schwarz ist minimizing spieler
        timeLimit = (int)Math.Round(timer.IncrementMilliseconds + timer.MillisecondsRemaining * 0.1);
        isWhite = board.IsWhiteToMove;
        evaluatedPositions = 0;
        transposCutoffs = 0;
        reachedDepth = 0;
        
        //Starte iterative tiefensuche
        var (bestMove, bestEval) = IterativeDeepening(board,timer);
        Console.WriteLine("EVIL: Evaluated {0} Positions,Reached Depth: {3}, Transposition Cutoffs: {1}, Best value: {2}",evaluatedPositions,transposCutoffs,bestEval,reachedDepth);
        //Console.WriteLine(board.CreateDiagram());
        if (!board.GetLegalMoves().Contains(bestMove))
        {
            bestMove = board.GetLegalMoves()[0];
        }
        lastBestMove = bestMove;
        
        return bestMove;
    }

    private (Move,int) AlphaBeta(Board board, int depth, int alpha, int beta, bool maximizingPlayer, Timer timer)
    {
        Move[] legalMoves = board.GetLegalMoves();
        legalMoves = OrderMoves(legalMoves, board);
        
        if (depth == 0 || board.IsInCheckmate()||board.IsDraw())
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
                if (timer.MillisecondsElapsedThisTurn >= timeLimit)
                {
                    return (bestMove, maxEval);
                }
                
                board.MakeMove(move);
                
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
                    transposCutoffs++;
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
                if (timer.MillisecondsElapsedThisTurn >= timeLimit)
                {
                    return (bestMove, minEval);
                }
               
                board.MakeMove(move);
               
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
                   transposCutoffs++;
                   break;
               }
            }

            return (bestMove, minEval);
        }
    }

    private (Move, int) IterativeDeepening(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        var bestEval = 0;
        for (int depht = 1; depht < MAX_DEPHT; depht++)
        {
            if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            {
                break;
            }

            var (move, eval) = AlphaBeta(board, depht, int.MinValue, int.MaxValue, isWhite,timer);
            bestMove = move;
            bestEval = eval;
            reachedDepth++;
        }

        return (bestMove,bestEval);
    }

    private int evaluate(Board board, int remainingDepth)
    {
        evaluatedPositions++;
        var zobrisKey = board.ZobristKey;
        if (transpositionTable.ContainsKey(zobrisKey) && transpositionTable[zobrisKey].Depht >= remainingDepth)
        {
            transposCutoffs++;
            return transpositionTable[zobrisKey].Value;
        }
        
        if (board.IsInCheckmate())
        {
            return board.IsWhiteToMove ? Int32.MinValue : Int32.MaxValue;
        }

        int wPieceScore = 0, bPieceScore=0,legalMoves=board.GetLegalMoves().Length * 5, isCheck = board.IsInCheck() ? 50 : 0,castleRights = (board.HasKingsideCastleRight(board.IsWhiteToMove)||board.HasQueensideCastleRight(board.IsWhiteToMove))?20:0;


        var allPieces = board.GetAllPieceLists();
        foreach (var pieceList in allPieces)
        {
            if (pieceList.Count > 0)
            {
                if (pieceList[0].IsWhite)
                {
                    wPieceScore += pieceValues[(int)pieceList[0].PieceType] * (pieceList.Count);

                }
                else
                {
                    bPieceScore += pieceValues[(int)pieceList[0].PieceType] * (pieceList.Count);
                }
            }
        }

        int score = 2 * (wPieceScore - bPieceScore) + legalMoves + isCheck+castleRights;
        store(zobrisKey,new TranspositionEntry {Value = (short)(score),Depht = (byte)remainingDepth});
        return score;
    }

    private Move[] OrderMoves(Move[] moves, Board board)
    {
        // Nehmen Sie den besten letzten Zug vorweg, falls er vorhanden ist
        List<Move> orderedMoves = new List<Move>();
        if (lastBestMove != Move.NullMove && moves.Contains(lastBestMove))
        {
            orderedMoves.Add(lastBestMove);
        }

        // Sortieren Sie die restlichen Züge
        var sortedMoves = moves.OrderByDescending(move =>
        {
            board.MakeMove(move);
            int moveValue = 0;

            if (board.IsInCheck())
            {
                moveValue = 10000;
            }
            board.UndoMove(move);
            if (move.IsCapture)
            {
                moveValue = pieceValues[(int)board.GetPiece(move.TargetSquare).PieceType] 
                            - pieceValues[(int)board.GetPiece(move.StartSquare).PieceType];
            }
            
            return moveValue;
        }).ToArray();

        orderedMoves.AddRange(sortedMoves);

        return orderedMoves.ToArray();
    }

    void store(ulong key, TranspositionEntry entry)
    {
        if (transpositionTable.Count >= MAX_ENTRIES)
        {
            transpositionTable.Remove(transpositionTable.Keys.First()); 
        }

        transpositionTable[key] = entry;
    }
}