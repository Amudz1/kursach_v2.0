<?php

return [
    'default' => env('CACHE_DRIVER', 'database'),

    'stores' => [
        'database' => [
            'driver'     => 'database',
            'table'      => env('DB_CACHE_TABLE', 'cache'),
            'connection' => env('DB_CONNECTION', 'pgsql'),
            'lock_connection' => env('DB_CONNECTION', 'pgsql'),
        ],
        'file' => [
            'driver' => 'file',
            'path'   => storage_path('framework/cache/data'),
        ],
    ],

    'prefix' => env('CACHE_PREFIX', 'aibudz_cache'),
];
