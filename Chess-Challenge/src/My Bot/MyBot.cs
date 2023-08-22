using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

//todo: transpos table
//todo: iterative deepening
//todo: king safety and endgame ? 
//todo: 
public class MyBot : IChessBot
{
    //settings
    private const int MAX_DEPTH = 7;
    private const int infinity = 99999999;
    private const int MAX_ENTRIES = 13000000;
    
    //control variables
    private int evaluatedPositions;

    private int cutoffAlphaBeta;

    private int cutoffTT;
    //manditory variables
    Move bestMoveThisPosition = Move.NullMove;
    Move bestMoveThisIteration = Move.NullMove;
    int[] pieceValues = {100,320,330,500,900,20000};
    private Dictionary<ulong, TranspositionEntry> transpositionTable = new();
    
    public Move Think(Board board, Timer timer)
    {
        evaluatedPositions = 0;
        cutoffAlphaBeta = 0;
        cutoffTT = 0;
        
        Search(board, MAX_DEPTH, -infinity, infinity,0);

        Console.WriteLine("MYBOT: Evaluated: {0}, Beta-Cuttoffs: {1}, TT-Cutoffs: {2}",evaluatedPositions, cutoffAlphaBeta, cutoffTT);
        Console.WriteLine("MYBOT: Best move is: " + bestMoveThisIteration);
        return bestMoveThisIteration;
    }

    private int Search(Board board, int depth, int alpha, int beta, int plyFromRoot)
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
        
        //max. depth starts evaluating board position, todo: quisence serach
        if (depth == 0) return Evaluate(board);
        
        Move[] moves = board.GetLegalMoves();
        
        if (moves.Length == 0 || board.IsDraw())
        {
            if (board.IsInCheck()) return -9999999; //neg infinity

            return 0;
        }

        foreach (var move in moves)
        {
            board.MakeMove(move);
            var evaluation = -Search(board,depth - 1, -beta, -alpha,plyFromRoot+1);
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

    int Evaluate(Board board)
    {
        evaluatedPositions++;
        return getMaterialDifference(board);
    }

    int getMaterialDifference(Board board)
    {
        var sum = 0;
        PieceList[] pieceLists = board.GetAllPieceLists();
        for (int i = 0; i < pieceLists.Length-1; i++)
        {
            if (i == 5) continue;
            
            sum += (i < 6) ? pieceLists[i].Count * pieceValues[i] : -pieceLists[i].Count * pieceValues[i-6];
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