clear; clc;


% Nodes coordinates 
coords_player = [1 2
                 1 3];   


% Elements (node i, node j)
elems_player = [4 7
                5 6
                1 5
                4 6
                6 3
                3 7
                6 7
                ];

[Level, Player, Forces, Percentages, Deformation] = Function6(coords_player, elems_player);
