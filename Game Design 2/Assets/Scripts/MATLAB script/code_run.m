clear; clc;

Mass = 40000; 
side_width = 0.05;

% Nodes coordinates 
coords = [0 0 
        1 0   
        2 1  
        1 2   
        0 1  
        1 1  
        ];   

% Elements (node i, node j)
elems = [1 5   
        5 6  
        4 6   
        2 6  
        1 6   
        4 5   
        3 6   
        3 4
        ];

[Forces, Percentages, Deformation] = code_function(Mass, side_width, coords, elems);