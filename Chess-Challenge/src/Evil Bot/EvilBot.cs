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
public class EvilBot : IChessBot
{
    //settings
    const int MAX_DEPTH = 20;
    const int infinity = 99999999;
    private const int TIME_LIMIT = 100;
    
    //control variables
    int evaluatedPositions;
    int cutoffAlphaBeta;
    int cutoffTT;
    int quiesenceSearched;
    
    //manditory variables
    Move bestMoveThisIteration;
    Move bestMoveSoFar;
    int[] pieceValues = {0,100,320,330,500,900,20_000};
    int[] moveValues;
    int round;

    TranspositionTableEntry?[] transpositionTable;
    private int ENTRY_AMOUNT = 1_000_000;

    const byte EXACT = 0;
    const byte UPPERBOUND = 1;
    const byte LOWERBOUND = 2;

    public Move Think(Board board, Timer timer)
    {
        evaluatedPositions = 0;
        cutoffAlphaBeta = 0;
        cutoffTT = 0;
        quiesenceSearched = 0;
        moveValues = new int[218];
        transpositionTable = new TranspositionTableEntry[ENTRY_AMOUNT];

        
        
        IterativeDeepening(board,timer);
        
        round++;
        return bestMoveSoFar;
    }

    void IterativeDeepening(Board board, Timer timer)
    {
        for (int i = 1; i <= MAX_DEPTH; i++)
        {
            Search(board, i, -infinity, infinity, 0, timer);
            
            bestMoveSoFar = bestMoveThisIteration;
            bestMoveThisIteration = Move.NullMove;

            if (timer.MillisecondsElapsedThisTurn >= TIME_LIMIT)
            {
                //Console.WriteLine("MYBOT: Searched to depth of: "+ i);
                break;
            }
        }
        
    }

    int Search(Board board, int depth, int alpha, int beta, int plyFromRoot, Timer timer)
    {
        if (timer.MillisecondsElapsedThisTurn >= TIME_LIMIT)
        {
            return 0;
        }

        if (depth == 0) return QuiescenceSearch(alpha, beta, board);
        
        int originalAlpha = alpha;
        //Transposition table allows the skip of similar positions.
        var zobrisKey = board.ZobristKey;
        TranspositionTableEntry? entry = transpositionTable[zobrisKey % (ulong)ENTRY_AMOUNT];
        if (entry != null && entry.Key == zobrisKey && entry.Depth >= depth) //todo: since the depth goes down in my algorithm it might be <= or >=
        {
            cutoffTT++;
            byte flag = entry.Flag;
            if (flag == EXACT) return entry.Value;
            if (flag == LOWERBOUND)
            {
                alpha = Math.Max(alpha, entry.Value);
            }else if (flag == UPPERBOUND) beta = Math.Min(beta, entry.Value);

        }

        if (alpha >= beta)
        {
            return entry.Value;
        }
        //max. depth starts evaluating board position
        if (depth == 0) return QuiescenceSearch(alpha,beta,board);

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);
        
        moves = OrderMoves(board.GetLegalMoves(), board);
        
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
                alpha = evaluation;
                if (plyFromRoot == 0)
                {
                    bestMoveThisIteration = move;
                }
            }
        }
        //set the flag
        byte storeFlag = 0;
        if (alpha <= originalAlpha) storeFlag = UPPERBOUND;
        if (alpha >= beta) storeFlag = LOWERBOUND;
        
        //store the entry in the table
        transpositionTable[zobrisKey % (ulong)ENTRY_AMOUNT] = new TranspositionTableEntry {Key = zobrisKey, Depth = (byte)depth,Flag = storeFlag,Value = alpha};
        
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
    Span<Move> OrderMoves(Span<Move> moves, Board board)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            
            var move = moves[i];
            var movePieceType = board.GetPiece(move.TargetSquare).PieceType;
            var capturePieceType = board.GetPiece(move.StartSquare).PieceType;
            int score = 0;

            if (move.Equals(bestMoveSoFar))
            {
                score += infinity;
            }
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
        evaluatedPositions++;

        var perspective = board.IsWhiteToMove ? 1 : -1;
        var value = 0;
        
        if (board.IsInCheckmate())
        {
            return infinity;
        }
        if (board.IsDraw())
        {
            value -= 10000;
        }
        /*
        if (round < 10)
        {
            value += board.HasKingsideCastleRight(board.IsWhiteToMove) ||
                      board.HasQueensideCastleRight(board.IsWhiteToMove)
                ? 20
                : 0;
            
        }
        value += board.IsInCheck() ? 20 : 0;
        Span<Move> evalMoves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref evalMoves);
        
        value += evalMoves.Length*2;
        */
        return perspective*GetMaterialDifference(board)+value;
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
