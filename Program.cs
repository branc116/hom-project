Console.WriteLine( (args[0], args[1]) );
RunInstance( args[0], args[1] );

void RunInstance( string pathIn, string pathOut ) {
    //var oggame =Help.GetGame( args[0] );
    var best_route = new List<Route>();
    var trucks = int.MaxValue;
    Game? best_game = null;
    
    for ( int k = 0 ; k < 10 ; k++ ) {
        var midPoints = new List<Point>();
        var game = Help.GetGame( args[0] );
        var ogGame = game;
        var i = 1;
        var outstr = "";
        var dt = 0.0;
        var cr = new List<Route>();
        while ( game.Cs.Length > 1 ) {
            var route = game.GetBestRoute( double.Parse(args[2]) );
            game = game.WithoutRout( route );
            outstr += $"{i}: {route}\n";
            dt += route.GetDistance( );
            ++i;
            cr.Add( route );
            midPoints.Add( route.MidPoint( ) );
        }
        var distMatrix = (from mp1 in midPoints
                          from mp2 in midPoints
                          select mp1.DistanceTo(mp2)).ToList();
        for ( int a = 0 ; a < distMatrix.Count ; ++a ) {
            var c = cr[a / midPoints.Count];
            if ( a % midPoints.Count == 0 )
                Console.WriteLine((c.Customers.Sum(c => c.demand), c.Customers.Count, c.SlackTime()));
            Console.Write($"{distMatrix[a]:D3} ");
        }
        Console.WriteLine( );
        if ( cr.Count < trucks ) {
            trucks = cr.Count;
            best_route = cr;
            best_game = ogGame;
        }
        outstr = $"{i}\n{outstr}\n{dt}\n";
        //Console.WriteLine( outstr );
    }
    //File.WriteAllText( pathOut, ToString( best_route ) );
    //best_route.Dot( best_game ?? throw new Exception( "Shit fuck" ), $"{pathOut}.dot" );
}

string ToString( List<Route> routes ) {
    var s = $"{routes.Count}\n";
    var dist = 0.0;
    for ( int i = 0 ; i < routes.Count ; i++ ) {
        var r = routes[i];
        s += $"{i + 1}: {r}\n";
        dist += r.GetDistance( );
    }
    s += $"{dist}\n";
    return s;
}
public record Point( int X, int Y ) {
    public int DistanceTo( Point p ) => (int)Math.Ceiling( DistanceToD( p ) );
    public double DistanceToD( Point p ) => Math.Sqrt(
        ( X - p.X ) * ( X - p.X ) +
        ( Y - p.Y ) * ( Y - p.Y )
    );
    public static Point operator +( Point a, Point b ) {
        return new Point( a.X + b.X, a.Y + b.Y );
    }
    public static Point operator /( Point a, int b ) {
        return new Point( a.X / b, a.Y / b );
    }
}
public record Customer( int index, Point P,
    int demand, int ReadyTime, int DueDate, int ServiceTime );
public record Route( List<Customer> Customers ) {
    public string Details( ) {
        var s = "";
        var time = 0.0;
        var pos = Customers[0].P;
        for ( var i = 0 ; i < Customers.Count ; i++ ) {
            var c = Customers[i];
            time = Math.Max( time + pos.DistanceTo( c.P ), c.ReadyTime );
            s += $"{c.index}: [{c.ReadyTime}, {time}, {c.DueDate}] -> {time + c.ServiceTime}\n";
            time += c.ServiceTime;
            pos = c.P;
        }
        return s;
    }
    public IEnumerable<double> GetArriveTimes( ) {
        double time = 0;
        var pos = Customers[0].P;
        for ( var i = 0 ; i < Customers.Count ; i++ ) {
            var c = Customers[i];
            time = Math.Max( time + pos.DistanceTo( c.P ), c.ReadyTime );
            yield return time;
            time += c.ServiceTime;
            pos = c.P;
        }
    }
    public double GetDistance( ) {
        var l = Customers[0];
        var sum = 0.0;
        foreach ( var i in Customers ) {
            sum += l.P.DistanceToD( i.P );
            l = i;
        }
        return sum;
    }
    public Point MidPoint( ) {
        var r = new Point(0, 0);
        return Customers.Select( i => i.P ).Aggregate( ( i, j ) => i + j ) / Customers.Count;
    }
    public override string ToString( ) => Customers
        .Select( i => i.index )
        .Select( i => i.ToString( ) )
        .Zip( GetArriveTimes( ) )
        .Select( i => $"{i.First}({i.Second})" )
        .Aggregate( ( i, j ) => $"{i}->{j}" );
    public int SlackTime() {
        var ct = 0;
        var slack = 0;
        for (int i = 1 ; i < Customers.Count ; ++i ) {
            var c = Customers[i];
            var l = Customers[i - 1];
            var tt = l.P.DistanceTo( c.P );
            var rt = c.ReadyTime;
            slack += Math.Max( 0, rt - tt - ct );
            ct += Math.Max( ct + tt, rt ) + c.ServiceTime;
            
        }
        return slack;
    }

}
public record Game(
    int N,
    int C,
    Customer[] Cs,
    int[][] distances,
    (int arriveTime, List<int> visited)[] Visited,
    List<int> locations ) {
    public Game( int N, int C, Customer[] customers ) : this( N, C,
        customers,
        Distances( customers ),
        customers.Select( c => (int.MaxValue, new List<int>( )) ).ToArray( ),
        new List<int> { 0 }
    ) {
        Visited[0].visited.Add( 0 );
        Visited[0] = (0, Visited[0].visited);
        Console.WriteLine( $"{N} {C}" );
    }
    public Route GetBestRoute( double heet ) {
        while ( locations.Any( ) ) {
            Step( heet );
        }
        return new Route( Visited.MaxBy( i => i.visited.Count ).visited.Select( i => Cs[i] ).Append( Cs[0] ).ToList( ) );
    }
    public Game WithoutRout( Route r ) {
        var newCs = Cs.Where( c => c.index == 0 || !r.Customers.Any( rr => rr.index == c.index ) ).ToArray( );
        var newV = newCs.Select( c => (int.MaxValue, new List<int>( )) ).ToArray( );
        newV[0].Item2.Add( 0 );
        newV[0] = (0, newV[0].Item2);
        return this with {
            Cs = newCs,
            distances = Distances( newCs ),
            Visited = newV,
            locations = new List<int> { 0 }
        };
    }
    public void Step( double heat ) {
        var newLocations = new HashSet<int>();
        var heat2 = 1 / ( 1 / heat ) * ( 1 / heat );
        for ( int id = 0 ; id < Cs.Length ; id++ ) {
            var (old_at, v) = Visited[id];
            if ( old_at > 10e4 )
                continue;
            for ( var j = 0 ; j < Cs.Length ; ++j ) {
                old_at = Visited[id].arriveTime;
                if ( id == j )
                    continue;
                if ( v.Contains( j ) )
                    continue;
                var w = W(old_at, id, j);
                if ( w > 10e5 )
                    continue;
                if ( v.Select( p => Cs[p].demand ).Sum( ) + Cs[j].demand > C )
                    continue;

                var at = Math.Max(old_at + Cs[id].ServiceTime + distances[id][j], Cs[j].ReadyTime);
                if (
                    //Random.Shared.NextDouble( ) < heat && 
                    (
                        (
                            (
                                Random.Shared.NextDouble( ) > heat && Visited[j].visited.Count == ( v.Count ) ||
                                Random.Shared.NextDouble( ) > heat2 && Visited[j].visited.Count + 1 == v.Count ||
                                Visited[j].visited.Count == ( v.Count + 1 )
                            ) &&
                            at < Visited[j].arriveTime
                        ) ||
                        Visited[j].visited.Count < ( v.Count + 1 )
                        )
                    ) {
                    var nl = v.Append( j ).ToList( );
                    var at_check =GetArriveTime( nl );
                    //if ( Math.Abs( at - at_check ) > 0.0001 )
                    //    throw new Exception( );
                    //if ( !IsRouteValid( nl ) )
                    //    throw new Exception( "Shit fuck" );
                    Visited[j] = (at, nl);
                    newLocations.Add( j );
                }
            }
        }
        locations.Clear( );
        locations.AddRange( newLocations );
    }
    public int W( int ai, int i, int j ) {
        if ( ai > Cs[i].DueDate )
            return int.MaxValue;
        if ( ai + Cs[i].ServiceTime + distances[i][j] > Cs[j].DueDate )
            return int.MaxValue;
        if ( Math.Max( Cs[j].ReadyTime, ai + Cs[i].ServiceTime + distances[i][j] ) + Cs[j].ServiceTime + distances[j][0] > Cs[0].DueDate )
            return int.MaxValue;
        return Math.Max( 0, Cs[j].ReadyTime - ai - Cs[i].ServiceTime ) + distances[i][j];
    }
    public static int[][] Distances( Customer[] customers ) {
        int[][] vs = new int[customers.Length][];
        for ( int i = 0 ; i < customers.Length ; i++ ) {
            vs[i] = new int[customers.Length];
            for ( int j = 0 ; j < customers.Length ; j++ ) {
                vs[i][j] = customers[i].P.DistanceTo( customers[j].P );
            }
        }
        return vs;
    }
    public double GetArriveTime( List<int> cs ) {
        var time = 0.0;
        var pos = Cs[0].P;
        var count = cs.Count();
        for ( var i = 0 ; i < count ; i++ ) {
            var c = Cs[cs[i]];
            time = Math.Max( time + pos.DistanceTo( c.P ), c.ReadyTime );
            time += c.ServiceTime;
            pos = c.P;
        }
        time -= Cs[^1].ServiceTime;
        return time;
    }
    public bool IsRouteValid( List<int> cs ) {
        var time = 0.0;
        var pos = Cs[0].P;
        var count = cs.Count();
        for ( var i = 0 ; i < count ; i++ ) {
            var c = Cs[cs[i]];
            time = Math.Max( time + pos.DistanceTo( c.P ), c.ReadyTime );
            if ( time > c.DueDate )
                return false;
            time += c.ServiceTime;
            pos = c.P;
        }
        return time < Cs[0].DueDate;
    }
}
public static class Help {
    public static Game GetGame( this string path ) {
        var lines = System.IO.File.ReadAllLines(path).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => !s.Any(c => c >= 'A' && c <= 'Z'))
            .Select(i => i.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(i => int.Parse(i)).ToList())
            .ToList();
        var nc = lines.First();
        var (n, c) = (nc[0], nc[1]);
        var customers = lines.Skip(1).Select(i => new Customer(i[0], new (i[1], i[2]), i[3], i[4], i[5], i[6])).ToArray()
            .OrderBy(i => i.index == 0 ? int.MinValue : Random.Shared.Next())
            .ToArray();
        return new Game( n, c, customers );
    }
    public static string DotLocations( this Game game ) {
        var ret = "";
        foreach ( var c in game.Cs ) {
            ret += $"N{c.index} [pos = \"{c.P.X},{c.P.Y}\"]\n";
        }
        return ret;
    }
    public static void Dot( this List<Route> routes, Game game, string path ) {

        var ret = "digraph D {\n" + game.DotLocations();
        foreach ( var item in routes ) {
            var last = item.Customers[0];
            foreach ( var c in item.Customers.Skip( 1 ) ) {
                ret += $"N{last.index}->N{c.index};\n";
                last = c;
            }
        }
        File.WriteAllText( path, ret + "}\n" );
    }
    public static void WriteDotFileVisited( this Game game, string path ) {
        string g = "digraph D {\n";
        var hashSet = new HashSet<(int, int, int)>();
        for ( int i = 0 ; i < game.Visited.Length ; ++i ) {
            var (at, v) = game.Visited[i];
            if ( at > 10e10 )
                continue;
            int old = 0;
            for ( int j = 0 ; j < v.Count ; ++j ) {
                hashSet.Add( (old, v[j], j) );
                old = v[j];
            }
        }
        var l = hashSet.ToList();
        for ( int i = 0 ; i < hashSet.Count ; i++ )
            g += $"N{l[i].Item3}_{l[i].Item1} -> N{l[i].Item3 + 1}_{l[i].Item2};\n";
        g += "}\n";
        File.WriteAllText( path, g );
    }
    public static void WriteDotFile( this Game game, string path ) {
        string g = "digraph D {\n";
        for ( int i = 0 ; i < game.Cs.Length ; ++i ) {
            var bc = game.Cs.Where(j => j.index != i).MinBy(j => game.W(game.Cs[i].ReadyTime, i, j.index));
            g += $"N{i} -> N{bc.index};\n";
            bc = game.Cs.Where( j => j.index != i && j.index != bc.index ).MinBy( j => game.W( game.Cs[i].ReadyTime, i, j.index ) );
            g += $"N{i} -> N{bc.index};\n";
            //for ( int j = 0 ; j < game.Cs.Count ; ++j ) {
            //    if ( i == j )
            //        continue;
            //    var w1 = game.W( game.Cs[i].ReadyTime, i, j );
            //    if (w1 < 10e10) {
            //        g += $"N{i} -> N{j}[weight=\"{ w1}\"];\n";
            //    }
            //}
        }
        g += "}\n";
        File.WriteAllText( path, g );
    }
}
