Help.Instance = args[3];
Help.Log( (args[0], args[1]) );
RunInstance( args[0], args[1], args[3] );

void RunInstance( string pathIn, string pathOut, string instance ) {
    
    var heat = double.Parse(args[2]);
    Game best_game = Help.GetGame( args[0] );
    var best_route = best_game.GetBestRoutes( heat );
    
    var fileName = $"{pathOut}/res_{instance}_1m.txt";
    File.WriteAllText( fileName, ToString( best_route ) );
    best_route.Dot( best_game, $"{fileName}.dot" );
    Help.Log("1 min");

    fileName = $"{pathOut}/res_{instance}_5m.txt";
    best_route = best_game.LocalSearch( best_route, TimeSpan.FromMinutes( 5 ), heat );
    File.WriteAllText( fileName, ToString( best_route ) );
    best_route.Dot( best_game , $"{fileName}.dot" );
    Help.Log( "5 min" );
    
    fileName = $"{pathOut}/res_{instance}_un.txt";
    best_route = best_game.LocalSearch( best_route, TimeSpan.FromMinutes( 5 ), heat );
    File.WriteAllText( fileName, ToString( best_route ) );
    best_route.Dot( best_game , $"{fileName}.dot" );

    var mins = 11;
    while ( true ) {
        best_route = best_game.LocalSearch( best_route, TimeSpan.FromMinutes( 1 ), heat );
        fileName = $"{pathOut}/res_{instance}_{mins}m.txt";
        best_route.Dot( best_game , $"{fileName}.dot" );

        fileName = $"{pathOut}/res_{instance}_un.txt";
        File.WriteAllText( fileName, ToString( best_route ) );
        best_route.Dot( best_game , $"{fileName}.dot" );

        Help.Log( $"min {mins}, " );
        ++mins;
    }
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
public class Route {
    public Route( List<Customer> customers ) {
        Customers = customers;
    }
    public List<Customer> Customers { get; set; }
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
        slack += Customers[0].DueDate - ct;
        return slack;
    }
    public Route? TryInsertCustomer(Game g, Customer c) {

        var allRoutes = Enumerable.Range( 1, Customers.Count - 2 )
            .Select( i => Customers
                .Take( i )
                .Append( c )
                .Concat( Customers.Skip( i ) )
                .ToList( ) );
        var r = allRoutes
            .Where( i => g.IsRouteValid( i ) )
            .MinBy(i => i.Sum(i => i.demand));
        return r is not null ? new Route(r) : default;
    }
}
public record Game(
    int N,
    int C,
    Customer[] Cs,
    int[][] distances,
    (int arriveTime, int demand, List<int> visited)[] Visited,
    List<int> locations ) {
    public Game( int N, int C, Customer[] customers ) : this( N, C,
        customers,
        Distances( customers ),
        customers.Select( c => (int.MaxValue, 0, new List<int>( )) ).ToArray( ),
        new List<int> { 0 }
    ) {
        Visited[0].visited.Add( 0 );
        Visited[0] = (0, 0, Visited[0].visited);
    }
    public List<Route> GetBestRoutes( double heet) {
        var g = this;
        var r = new List<Route>();
        while(g.Cs.Length > 1) {
            var nr = g.GetBestRoute( heet );
            r.Add( nr );
            g = g.WithoutRout( nr );
        }
        return r;
    }
    public Route GetBestRoute( double heet ) {
        while ( locations.Any( ) ) {
            Step( heet );
        }
        return new Route( Visited.MaxBy( i => i.visited.Count ).visited.Select( i => Cs[i] ).Append( Cs[0] ).ToList( ) );
    }
    public Game WithoutRout( Route r ) {
        var newCs = Cs.Where( c => c.index == 0 || !r.Customers.Any( rr => rr.index == c.index ) ).ToArray( );
        var newV = newCs.Select( c => (int.MaxValue, 0, new List<int>( )) ).ToArray( );
        newV[0].Item3.Add( 0 );
        newV[0] = (0, 0, newV[0].Item3);
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
            var (old_at, demand, v) = Visited[id];
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
                if ( demand + Cs[j].demand > C )
                    continue;

                var at = Math.Max(old_at + Cs[id].ServiceTime + distances[id][j], Cs[j].ReadyTime);
                int count = Visited[j].visited.Count;
                if (
                    (
                        (
                            (
                                Random.Shared.NextDouble( ) > heat && count == ( v.Count ) ||
                                Random.Shared.NextDouble( ) > heat2 && count + 1 == v.Count ||
                                count == ( v.Count + 1 )
                            ) &&
                            at < Visited[j].arriveTime
                        ) ||
                        count < ( v.Count + 1 )
                        )
                    ) {
                    var nl = v.Append( j ).ToList( );
                    var at_check =GetArriveTime( nl );
                    Visited[j] = (at, demand + Cs[j].demand, nl);
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
        var dist = ai + Cs[i].ServiceTime + distances[i][j];
        if ( dist > Cs[j].DueDate )
            return int.MaxValue;
        if ( Math.Max( Cs[j].ReadyTime, dist ) + Cs[j].ServiceTime + distances[j][0] > Cs[0].DueDate )
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
    public bool IsRouteValid( List<Customer> cs ) {
        var time = 0.0;
        var pos = Cs[0].P;
        var count = cs.Count();
        for ( var i = 0 ; i < count ; i++ ) {
            var c = cs[i];
            time = Math.Max( time + pos.DistanceTo( c.P ), c.ReadyTime );
            if ( time > c.DueDate )
                return false;
            time += c.ServiceTime;
            pos = c.P;
        }
         
        return time < Cs[0].DueDate && cs.Sum( i => i.demand ) <= C;
    }
}
public static class Help {
    public static string Instance = "";
    public static void Log(object obj) {
        Console.WriteLine($"[{DateTime.Now}][{Instance}]: {obj}");
    }
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
    public static List<Route> LocalSearch(this Game game, List<Route> initSolutions, TimeSpan timeout, double heat = 1.0) {
        var st = DateTime.Now;
        var (best, best_d) = (initSolutions.Select(i => new Route(i.Customers.ToList()) ).ToList(), initSolutions.Sum(i => i.GetDistance()));
        var sinceReset = 0;
        int swaps = 0;
        void addIfBetter(List<Route> newRoutes) {
            var d = initSolutions.Sum(i => i.GetDistance());
            if ( best.Count > initSolutions.Count || ( best.Count == initSolutions.Count && best_d > d ) ) {
                best = initSolutions;
                best_d = d;
                Help.Log( (initSolutions.Count, best_d) );
            }
        }
        while ( ( DateTime.Now - st ) < timeout ) {
            for ( int i = 0 ; i < initSolutions.Count ; ++i ) {
                var smaller = initSolutions[i];
                Route? routeToAdd = null;
                for ( int j = 0 ; j < initSolutions.Count ; ++j ) {
                    if ( i == j )
                        continue;
                    var bigger = initSolutions[j];
                    routeToAdd = null;
                    if ( bigger.Customers.Count <= smaller.Customers.Count )
                        continue;
                    foreach ( var c in smaller.Customers.Where(i => i.index != 0) ) {
                        routeToAdd = bigger.TryInsertCustomer( game, c );
                        if ( routeToAdd is not null ) {
                            swaps++;
                            initSolutions[i] = new(smaller.Customers.Where(i => i.index != c.index).ToList());
                            break;
                        }
                    }
                    if ( routeToAdd is not null ) {
                        initSolutions[j] = routeToAdd;
                        if ( smaller.Customers.Count == 2 ) {
                            initSolutions = initSolutions.Where(i => i != smaller).ToList();
                        }
                        j = -1;
                        i = -1;
                        addIfBetter( initSolutions );
                        break;
                    }
                }
            }
            var ns = game.Mix( initSolutions, heat );
            while (ns == initSolutions) ns = game.Mix( initSolutions, heat );
            initSolutions = ns;
            addIfBetter( initSolutions );
            sinceReset++;
            if ( heat < Random.Shared.NextDouble( ) ) {
                initSolutions = best;
                Help.Log($"reset {sinceReset} {swaps}");
                swaps = 0;
                sinceReset = 0;
            }
        }
        return best;
    }
    public static List<Route> Mix(this Game game, List<Route> init, double heat) {
        var taken = init.OrderByDescending( i => Random.Shared.NextDouble( ) * i.SlackTime() )
            .Take( Random.Shared.Next( 2, 10 ) ).ToList();
        var rs = taken
            .SelectMany(i => i.Customers)
            .Where(i => i.index != 0)
            .OrderBy(i => Random.Shared.Next())
            .Prepend(game.Cs[0])
            .ToArray();
        var ret = init.Except(taken).ToList();
        var g = new Game(game.N, game.C, rs);
        ret = ret.Concat(g.GetBestRoutes( heat )).ToList();
        return ret.Count <= init.Count ? 
            ret : heat < Random.Shared.NextDouble( )  ? 
            ret : init;
    }
}
