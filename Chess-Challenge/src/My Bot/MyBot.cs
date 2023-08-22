using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

//todo: iterative deepening
//todo: king safety and endgame ? 
public class MyBot : IChessBot
{
    //settings
    private const int MAX_DEPTH = 6;
    private const int infinity = 99999999;
    private const int MAX_ENTRIES = 13000000;
    private const int timeLimit = 999999;
    
    //control variables
    private int evaluatedPositions;

    private int cutoffAlphaBeta;

    private int cutoffTT;
    //manditory variables
    Move bestMoveThisPosition = Move.NullMove;
    Move bestMoveThisIteration = Move.NullMove;
    private Move lastBestMove = Move.NullMove;
    int[] pieceValues = {0,100,320,330,500,900,20000};

    private int[] moveValues;
    private bool searchCancelled;
    
    private Dictionary<ulong, TranspositionEntry> transpositionTable = new();
    
    public Move Think(Board board, Timer timer)
    {
        
        evaluatedPositions = 0;
        cutoffAlphaBeta = 0;
        cutoffTT = 0;
        moveValues = new int[218];
        
        Search(board, MAX_DEPTH, -infinity, infinity,0,timer);
        //IterativeDeepening(board,timer);

        Console.WriteLine("MYBOT: Evaluated: {0}, Beta-Cuttoffs: {1}, TT-Cutoffs: {2}",evaluatedPositions, cutoffAlphaBeta, cutoffTT);
        Console.WriteLine("MYBOT: Best move is: " + bestMoveThisIteration);
        Console.WriteLine("Time took: "+ timer.MillisecondsElapsedThisTurn);
        return bestMoveThisIteration;
    }

    void IterativeDeepening(Board board, Timer timer)
    {
        var reachedDepth = 0;
        for (int searchDepth = 1; searchDepth < 256; searchDepth++)
        {
            Search(board, searchDepth, -infinity, infinity,0,timer);
            reachedDepth++;
            if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            {
                Console.WriteLine("Reached Depth: " + reachedDepth);
                lastBestMove = bestMoveThisIteration;
                break;
            }
        }

        
    }
    private int Search(Board board, int depth, int alpha, int beta, int plyFromRoot, Timer timer)
    {
        //searches if position has been evaluated before, if so the positions evaluation and move is read from the
        //transposition table
        var zobrisKey = board.ZobristKey;
        if (transpositionTable.ContainsKey(zobrisKey) && transpositionTable[zobrisKey].Depht >= depth)
        {
            cutoffTT++;
            if (plyFromRoot == 0)
            {
                bestMoveThisIteration = transpositionTable[zobrisKey].Move;
            }

            return transpositionTable[zobrisKey].Value;
        }
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
        {
            return 0;
        }
        //max. depth starts evaluating board position, todo: quisence serach
        if (depth == 0) return Evaluate(board);
        
        Move[] moves = OrderMoves(board.GetLegalMoves(),board);
        
        
        if (moves.Length == 0 || board.IsDraw())
        {
            if (board.IsInCheck()) return -9999999; //neg infinity

            return 0;
        }

        foreach (var move in moves)
        {
            board.MakeMove(move);
            var evaluation = -Search(board,depth - 1, -beta, -alpha,plyFromRoot+1,timer);
            board.UndoMove(move);
            
            //move is to good for the opposite side and gets pruned
            if (evaluation >= beta)
            {
                cutoffAlphaBeta++;
                return beta; //SNIPP
            }
            
            if(evaluation>alpha)
            {
                bestMoveThisPosition = move;
                alpha = evaluation;
                if (plyFromRoot == 0)
                {
                    bestMoveThisIteration = move;
                }
            }
        }
        store(board.ZobristKey,new TranspositionEntry {Value = (short)alpha,Depht = (byte)depth, Move = bestMoveThisPosition});
        return alpha;
    }

    private Move[] OrderMoves(Move[] moves, Board board)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            
            var move = moves[i];
            var movePieceType = board.GetPiece(move.TargetSquare).PieceType;
            var capturePieceType = board.GetPiece(move.StartSquare).PieceType;
            int score = 0;
            if (move.IsCapture)
            {
                var delta = pieceValues[(int)movePieceType] 
                                        - pieceValues[(int)capturePieceType];
                if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    score += (delta >= 0 ? 8000 : 2000) + delta;
                }
                else score += 8000 + delta;
            }

            if (movePieceType == PieceType.Pawn && move.IsPromotion && !move.IsCapture)
            {
                score += 6000;
            }

            moveValues[i] = score;
        }
        
        //sort the array
        for (int i = 0; i < moves.Length - 1; i++)
        {
            for (int j = i + 1; j > 0; j--)
            {
                int swapIndex = j - 1;
                if (moveValues[swapIndex] < moveValues[j])
                {
                    (moves[j], moves[swapIndex]) = (moves[swapIndex], moves[j]);
                    (moveValues[j], moveValues[swapIndex]) = (moveValues[swapIndex], moveValues[j]);
                }
            }
        }

        return moves;
    }

    int Evaluate(Board board)
    {
        int perspective = board.IsWhiteToMove ? 1 : -1;
        evaluatedPositions++;
        var value = 0;
        if (board.IsInCheckmate())
        {
            return infinity;
        }
        if (board.IsDraw())
        {
            return 0;
        }

        //value += board.GetLegalMoves().Length * 5;
        value += board.GetKingSquare(board.IsWhiteToMove).File == (6|2)? 20: 0;
        value += getMaterialDifference(board);
        
        
        return value*perspective;
    }

    int getMaterialDifference(Board board)
    {
        var sum = 0;
        PieceList[] pieceLists = board.GetAllPieceLists();
        for (int i = 0; i < pieceLists.Length-1; i++)
        {
            if (i == 5) continue;
            
            sum += (i < 6) ? pieceLists[i].Count * pieceValues[i+1] : -pieceLists[i].Count * pieceValues[i-5];
        }

        return sum;
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

public class TranspositionEntry
{
    public short Value { get; set; }
    public byte Depht { get; set; }
    public Move Move { get; set; }
}