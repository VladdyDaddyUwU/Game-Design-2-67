clear; clc;


% Nodes coordinates 
coords_player = [1 2
                ];   


% Elements (node i, node j)
elems_player = [3 4
                2 4
                2 5
                5 3
                4 5
                1 5
                ];

[scale, Level, Player, Forces, Percentages, Deformation] = Function8(coords_player, elems_player);
