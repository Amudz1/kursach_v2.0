<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;

class DatabaseSeeder extends Seeder
{
    public function run(): void
    {
        $this->call([
            SubscriptionPlanSeeder::class,
            UserSeeder::class,
            SubscriptionSeeder::class,
            ChatSeeder::class,
            MessageSeeder::class,
        ]);
    }
}
