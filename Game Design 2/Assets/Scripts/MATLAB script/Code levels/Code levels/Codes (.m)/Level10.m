clear; clc;


% Nodes coordinates 
coords_player = [2 2
                 2 3
                 2 1
                 1 2
                 3 2
                 ];   


% Elements (node i, node j)
elems_player = [1 7
                7 6
                1 6   
                4 7
                2 6
                4 6  
                5 4   
                3 8
                4 8
                5 7
                5 8
                3 5
                ];

[Level, Player, Forces, Percentages, Deformation] = Function10(coords_player, elems_player);
