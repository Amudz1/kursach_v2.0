<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class ChatSeeder extends Seeder
{
    public function run(): void
    {
        DB::table('chats')->truncate();

        $userIds = DB::table('users')->orderBy('id')->pluck('id');

        $chats = [
            ['title' => 'Помоги написать резюме',         'msg_count' => 6,  'days_ago' => 1],
            ['title' => 'Объясни принцип работы JWT',     'msg_count' => 4,  'days_ago' => 3],
            ['title' => 'Идеи для дипломной работы',      'msg_count' => 10, 'days_ago' => 5],
            ['title' => 'Переведи текст на английский',   'msg_count' => 3,  'days_ago' => 2],
            ['title' => 'Напиши SQL-запрос для отчёта',   'msg_count' => 7,  'days_ago' => 7],
            ['title' => 'Разбор ошибки в Python-коде',   'msg_count' => 5,  'days_ago' => 10],
            ['title' => 'Сгенерируй тест-кейсы',          'msg_count' => 8,  'days_ago' => 14],
            ['title' => 'Краткий пересказ статьи',        'msg_count' => 2,  'days_ago' => 0],
            ['title' => 'Как настроить Nginx + Laravel?', 'msg_count' => 9,  'days_ago' => 20],
            ['title' => 'Написание unit-тестов PHPUnit',  'msg_count' => 11, 'days_ago' => 4],
        ];

        $now = now();
        foreach ($chats as $i => $chat) {
            $userId = $userIds[$i % count($userIds)];
            $lastMsgAt = now()->subDays($chat['days_ago']);

            DB::table('chats')->insert([
                'user_id'         => $userId,
                'title'           => $chat['title'],
                'message_count'   => $chat['msg_count'],
                'last_message_at' => $lastMsgAt,
                'created_at'      => $lastMsgAt->subMinutes(rand(5, 120)),
                'updated_at'      => $lastMsgAt,
            ]);
        }
    }
}
