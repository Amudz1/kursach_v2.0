<?php

namespace App\Policies;

use App\Models\{User, Chat};

class ChatPolicy
{
    public function view(User $user, Chat $chat): bool
    {
        return $user->id === $chat->user_id;
    }

    public function delete(User $user, Chat $chat): bool
    {
        return $user->id === $chat->user_id;
    }
}
