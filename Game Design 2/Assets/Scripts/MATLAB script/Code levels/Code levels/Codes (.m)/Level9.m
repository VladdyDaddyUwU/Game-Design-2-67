clear; clc;


% Nodes coordinates 
coords_player = [1 2
                 2 2
                 1 3
                 2 3
                 ];   


% Elements (node i, node j)
elems_player = [1 6
                4 5
                6 7
                4 6
                5 7
                4 7
                1 4
                2 6
                2 4
                5 3
                7 3

                ];

[Level, Player, Forces, Percentages, Deformation] = Function9(coords_player, elems_player);
