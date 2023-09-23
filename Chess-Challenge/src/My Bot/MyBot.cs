using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

/// <summary>
/// This chess bot was created as participation in the "Tiny chess bot challenge" by Sebastian Lague. It uses the API of the given Project that already implements all features of chess.
/// This bot implements the following features (due to the restrictions of tokens in this challenge not all features can be implemented):
/// For search:
/// - Alpha-Beta Pruning
/// - Todo: Move Ordering - after capture values, promotion
/// - Todo: Transposition Tables
/// - Todo: Quiescence Search
/// - Todo: Iterative deepening
/// For evaluation:
/// - Material difference
/// - Todo: Checkmate, Check or Draw
/// - Todo: King safety
/// - Todo: King for endgame
/// </summary>
public class MyBot : IChessBot
{
    //settings
    const int MAX_DEPTH = 7;
    const int infinity = 99999999;
    
    //control variables
    int evaluatedPositions;
    int cutoffAlphaBeta;
    int cutoffTT;
    int quiesenceSearched;
    
    //manditory variables
    Move bestMoveThisPosition = Move.NullMove; //only used with transposition table
    Move bestMoveThisIteration = Move.NullMove;
    int[] pieceValues = {0,100,320,330,500,900,20000};
    int[] moveValues;
    
    public Move Think(Board board, Timer timer)
    {
        evaluatedPositions = 0;
        cutoffAlphaBeta = 0;
        cutoffTT = 0;
        quiesenceSearched = 0;
        moveValues = new int[218];

        
        Console.WriteLine("EVALUATION: "+ Evaluate(board));
        Search(board, MAX_DEPTH, -infinity, infinity,0);

        Console.WriteLine("MYBOT: Evaluated: {0}, Beta-Cuttoffs: {1}, TT-Cutoffs: {2}, Quiesence moves: {3}",evaluatedPositions, cutoffAlphaBeta, cutoffTT,quiesenceSearched);
        Console.WriteLine("MYBOT: Best move is: " + bestMoveThisIteration);
        return bestMoveThisIteration;
    }

    int Search(Board board, int depth, int alpha, int beta, int plyFromRoot)
    {
        
        //max. depth starts evaluating board position, todo: quisence serach
        if (depth == 0) return QuiescenceSearch(alpha,beta,board);

        var moves = OrderMoves(board.GetLegalMoves(), board);
        
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
        return alpha;
    }

    // Searches until the position is "quiet" (no more captures can be made)
    // In this format it uses a lot of tokens, could be integrated in the Search function.
    int QuiescenceSearch(int alpha, int beta, Board board)
    {
        quiesenceSearched++;
        int eval = Evaluate(board);
        
        if (eval >= beta)
        {
            cutoffAlphaBeta++;
            return beta;
        }
        if (eval > alpha)
        {
            alpha = eval;
        }

        var moves = board.GetLegalMoves(true);
        foreach (var move in moves)
        {
            board.MakeMove(move);
            eval = -QuiescenceSearch(-beta, -alpha, board);
            board.UndoMove(move);
            
            if (eval >= beta)
            {
                cutoffAlphaBeta++;
                return beta;
            }
            if (eval > alpha)
            {
                alpha = eval;
            }
        }

        return alpha;
    }

    /// Sorts the moves depending on how good they are, which makes alpha beta pruning a lot more efficient.
    /// This also has effects on how the bot plays. Eg. checkmate although it does not get evaluated that this is good.
    /// Downside: Consumes a lot of brain capacity. Todo: Move ordering with less tokens
    Move[] OrderMoves(Move[] moves, Board board)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            
            var move = moves[i];
            var movePieceType = board.GetPiece(move.TargetSquare).PieceType;
            var capturePieceType = board.GetPiece(move.StartSquare).PieceType;
            int score = 0;
            
            //checks if move is a capture, the won material difference and if the piece can be captured from the target square
            if (move.IsCapture && !capturePieceType.Equals(PieceType.King))
            {
                var delta = pieceValues[(int)movePieceType] 
                            - pieceValues[(int)capturePieceType];
                if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    score += (delta >= 0 ? 8000 : 2000) + delta;
                }
                else score += 8000 + delta;
            }

            //checks if piece can be promoted and not be captured
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
        var perspective = board.IsWhiteToMove ? 1 : -1;

        evaluatedPositions++;
        return perspective*GetMaterialDifference(board);
    }

    int GetMaterialDifference(Board board)
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
}

public class TranspositionEntryEvil
{
    public short Value { get; set; }
    public byte Depht { get; set; }
    public Move Move { get; set; }
}