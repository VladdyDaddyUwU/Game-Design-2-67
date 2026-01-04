clear; clc;


% Nodes coordinates 
coords_player = [0 2
                 1 2
                 1 3
                 2 3];   


% Elements (node i, node j)
elems_player = [1 5
                4 5
                4 6
                6 7
                5 6
                8 7 
                1 4
                5 7
                6 8
                6 3
                8 3
                ];

[Level, Player, Forces, Percentages, Deformation] = Function7(coords_player, elems_player);
