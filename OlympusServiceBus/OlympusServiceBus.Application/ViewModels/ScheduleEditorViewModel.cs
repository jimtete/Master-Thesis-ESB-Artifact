using System.ComponentModel;
using System.Runtime.CompilerServices;
using OlympusServiceBusApplication.Models.Contracts;

namespace OlympusServiceBusApplication.ViewModels;

public class ScheduleEditorViewModel : INotifyPropertyChanged
{
    private string _mode = "Manual";
    private DateTime _adHocDate = DateTime.Today;
    private string _adHocTime = "09:00";
    private string _timeZone = "UTC";
    private int _intervalValue = 1;
    private string _intervalUnit = "Minutes";
    private string _cronExpression = string.Empty;

    public string Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsManual));
            OnPropertyChanged(nameof(IsAdHoc));
            OnPropertyChanged(nameof(IsInterval));
            OnPropertyChanged(nameof(IsRecurring));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    public bool IsManual => string.Equals(Mode, "Manual", StringComparison.OrdinalIgnoreCase);
    public bool IsAdHoc => string.Equals(Mode, "AdHoc", StringComparison.OrdinalIgnoreCase);
    public bool IsInterval => string.Equals(Mode, "Interval", StringComparison.OrdinalIgnoreCase);
    public bool IsRecurring => string.Equals(Mode, "Recurring", StringComparison.OrdinalIgnoreCase);

    public DateTime AdHocDate
    {
        get => _adHocDate;
        set
        {
            if (_adHocDate == value) return;
            _adHocDate = value;
            OnPropertyChanged();
        }
    }

    public string AdHocTime
    {
        get => _adHocTime;
        set
        {
            if (_adHocTime == value) return;
            _adHocTime = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    public string TimeZone
    {
        get => _timeZone;
        set
        {
            if (_timeZone == value) return;
            _timeZone = value;
            OnPropertyChanged();
        }
    }

    public int IntervalValue
    {
        get => _intervalValue;
        set
        {
            if (_intervalValue == value) return;
            _intervalValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    public string IntervalUnit
    {
        get => _intervalUnit;
        set
        {
            if (_intervalUnit == value) return;
            _intervalUnit = value;
            OnPropertyChanged();
        }
    }

    public string CronExpression
    {
        get => _cronExpression;
        set
        {
            if (_cronExpression == value) return;
            _cronExpression = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    public bool IsValid
    {
        get
        {
            if (IsManual)
            {
                return true;
            }

            if (IsAdHoc)
            {
                return TimeOnly.TryParse(AdHocTime, out _);
            }

            if (IsInterval)
            {
                return IntervalValue > 0;
            }

            if (IsRecurring)
            {
                return !string.IsNullOrWhiteSpace(CronExpression);
            }

            return false;
        }
    }

    public string ValidationMessage
    {
        get
        {
            if (IsManual)
            {
                return "Manual execution selected.";
            }

            if (IsAdHoc)
            {
                return TimeOnly.TryParse(AdHocTime, out _)
                    ? "AdHoc schedule ready."
                    : "Please enter time as HH:mm.";
            }

            if (IsInterval)
            {
                return IntervalValue > 0
                    ? "Interval schedule ready."
                    : "Interval value must be greater than 0.";
            }

            if (IsRecurring)
            {
                return !string.IsNullOrWhiteSpace(CronExpression)
                    ? "CRON schedule ready."
                    : "Please enter a CRON expression.";
            }

            return "Invalid schedule configuration.";
        }
    }

    public void LoadFromSchedule(ScheduleEditorRequest? schedule)
    {
        if (schedule is null)
        {
            Mode = "Manual";
            AdHocDate = DateTime.Today;
            AdHocTime = "09:00";
            TimeZone = "UTC";
            IntervalValue = 1;
            IntervalUnit = "Minutes";
            CronExpression = string.Empty;
            return;
        }

        Mode = string.IsNullOrWhiteSpace(schedule.Mode) ? "Manual" : schedule.Mode;

        if (schedule.RunAt is not null)
        {
            AdHocDate = schedule.RunAt.Value.LocalDateTime.Date;
            AdHocTime = schedule.RunAt.Value.LocalDateTime.ToString("HH:mm");
        }
        else
        {
            AdHocDate = DateTime.Today;
            AdHocTime = "09:00";
        }

        TimeZone = string.IsNullOrWhiteSpace(schedule.TimeZone) ? "UTC" : schedule.TimeZone;
        IntervalValue = schedule.IntervalValue <= 0 ? 1 : schedule.IntervalValue;
        IntervalUnit = string.IsNullOrWhiteSpace(schedule.IntervalUnit) ? "Minutes" : schedule.IntervalUnit;
        CronExpression = schedule.CronExpression ?? string.Empty;
    }

    public ScheduleEditorRequest BuildSchedule()
    {
        DateTimeOffset? runAt = null;

        if (IsAdHoc && TimeOnly.TryParse(AdHocTime, out var parsedTime))
        {
            var combined = AdHocDate.Date.Add(parsedTime.ToTimeSpan());
            runAt = new DateTimeOffset(combined);
        }

        return new ScheduleEditorRequest
        {
            Mode = Mode,
            RunAt = runAt,
            IntervalValue = IntervalValue,
            IntervalUnit = IntervalUnit,
            CronExpression = CronExpression.Trim(),
            TimeZone = TimeZone.Trim()
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}