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
public class EvilBotV3 : IChessBot
{
    //settings
    private const int MAX_DEPTH = 20;
    private const int infinity = 99999999;
    private const int MAX_ENTRIES = 13000000;
    private const int timeLimit = 100;
    
    //control variables
    private int evaluatedPositions;

    private int cutoffAlphaBeta;

    private int cutoffTT;

    private int quiescenceSearch;
    //manditory variables
    Move bestMoveThisPosition = Move.NullMove; //best move in a given position
    Move bestMoveThisIteration = Move.NullMove; //best move in this whole iteration form a search
    private Move bestLastMove = Move.NullMove; //move from each previous search from iterative deepening
    int[] pieceValues = {0,100,320,330,500,900,20000};

    private int[] moveValues;
    private bool searchCancelled;
    
    private Dictionary<ulong, TranspositionEntry> transpositionTable = new();
    
    public Move Think(Board board, Timer timer)
    {
        
        evaluatedPositions = 0;
        cutoffAlphaBeta = 0;
        cutoffTT = 0;
        quiescenceSearch = 0;
        moveValues = new int[218];
        
        //Search(board, MAX_DEPTH, -infinity, infinity,0,timer);
        IterativeDeepening(board,timer);

        Console.WriteLine("EVIL: Evaluated: {0}, Beta-Cuttoffs: {1}, TT-Cutoffs: {2}, Used Quiescence: {3}",evaluatedPositions, cutoffAlphaBeta, cutoffTT, quiescenceSearch);
        Console.WriteLine("EVIL: Best move is: " + bestLastMove);
        //Console.WriteLine("Time took: "+ timer.MillisecondsElapsedThisTurn);
        return bestLastMove;
    }

    void IterativeDeepening(Board board, Timer timer)
    {
        for (int i = 1; i < MAX_DEPTH; i++)
        {
            Search(board, i, -infinity, infinity, 0, timer);
            bestLastMove = bestMoveThisIteration;
            bestMoveThisIteration = Move.NullMove;

            if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            {
                Console.WriteLine("MYBOT: Searched to depth of: "+ i);
                break;
            }
        }
        
    }
    private int Search(Board board, int depth, int alpha, int beta, int plyFromRoot, Timer timer)
    {
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
        {
            return 0;
        }
        //max. depth starts evaluating board position, todo: quisence serach
        Move[] moves;
        
        if (depth == 0)
        {
            return Evaluate(board);
            moves = board.GetLegalMoves(true);
            quiescenceSearch++;
            if (moves.Length == 0)
            {
                return Evaluate(board);
            }
        }
        else
        {
            moves = OrderMoves(board.GetLegalMoves(),board);
            if (moves.Length == 0 || board.IsDraw()) //if there are no legal moves (and the Quiescence Search is not going, the game is over
            {
                if (board.IsInCheck()) return -9999999; //neg infinity

                return 0;
            }
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
        //store(board.ZobristKey,new TranspositionEntry {Value = (short)alpha,Depht = (byte)depth, Move = bestMoveThisPosition});
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
            if (move.Equals(bestLastMove))
            {
                score += infinity;
                moveValues[i] = score;
                continue;
            }
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
            return infinity*perspective;
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
    
}
