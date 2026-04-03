<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Hash;

class UserSeeder extends Seeder
{
    public function run(): void
    {
        DB::table('users')->truncate();

        $users = [
            [
                'username'          => 'admin',
                'email'             => 'admin@aibudz.dev',
                'password'          => Hash::make('Admin@12345'),
                'is_admin'          => true,
                'email_verified_at' => now(),
            ],
            [
                'username'          => 'alice_dev',
                'email'             => 'alice@example.com',
                'password'          => Hash::make('Alice@Pass1'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(30),
            ],
            [
                'username'          => 'bob_smith',
                'email'             => 'bob@example.com',
                'password'          => Hash::make('Bob@Pass22'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(15),
            ],
            [
                'username'          => 'carol_w',
                'email'             => 'carol@example.com',
                'password'          => Hash::make('Carol@Pass3'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(7),
            ],
            [
                'username'          => 'dan_r',
                'email'             => 'dan@example.com',
                'password'          => Hash::make('Dan@Pass444'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(60),
            ],
            [
                'username'          => 'eva_k',
                'email'             => 'eva@example.com',
                'password'          => Hash::make('Eva@Pass555'),
                'is_admin'          => false,
                'email_verified_at' => null,
            ],
            [
                'username'          => 'fedor_m',
                'email'             => 'fedor@example.com',
                'password'          => Hash::make('Fedor@Pass6'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(3),
            ],
            [
                'username'          => 'gina_p',
                'email'             => 'gina@example.com',
                'password'          => Hash::make('Gina@Pass77'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(21),
            ],
            [
                'username'          => 'hans_l',
                'email'             => 'hans@example.com',
                'password'          => Hash::make('Hans@Pass88'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(45),
            ],
            [
                'username'          => 'irina_n',
                'email'             => 'irina@example.com',
                'password'          => Hash::make('Irina@Pass9'),
                'is_admin'          => false,
                'email_verified_at' => now()->subDays(10),
            ],
        ];

        $now = now();
        foreach ($users as $user) {
            $userId = DB::table('users')->insertGetId(
                array_merge($user, ['remember_token' => null, 'created_at' => $now, 'updated_at' => $now])
            );

            // Каждому пользователю — запись в request_limits
            DB::table('request_limits')->insert([
                'user_id'    => $userId,
                'used_today' => rand(0, 8),
                'daily_limit' => 12,
                'reset_date' => now()->toDateString(),
                'created_at' => $now,
                'updated_at' => $now,
            ]);
        }
    }
}
