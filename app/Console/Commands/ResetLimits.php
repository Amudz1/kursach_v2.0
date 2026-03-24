<?php

namespace App\Console\Commands;

use App\Models\RequestLimit;
use Illuminate\Console\Command;

class ResetLimits extends Command
{
    protected $signature   = 'app:reset-limits';
    protected $description = 'Сброс дневных лимитов запросов для бесплатных пользователей';

    public function handle(): void
    {
        $count = RequestLimit::where('reset_date', '<', today())->update([
            'used_today' => 0,
            'reset_date' => today(),
        ]);

        $this->info("✓ Сброшено лимитов: {$count}");
    }
}
