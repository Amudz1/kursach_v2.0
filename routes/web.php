<?php

use Illuminate\Support\Facades\Route;

// Все маршруты отдаём Vue Router — SPA
Route::get('/{any?}', function () {
    return view('app');
})->where('any', '.*');
