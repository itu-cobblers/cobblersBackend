#!/usr/bin/env python3
"""Generate scripts/seed-tasks.sql from BootIT Day 1/2 content. Run: python3 scripts/_gen_seed.py"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(__file__).resolve().parent / "seed-tasks.sql"
SRC = OUT  # read day3 from current seed


def lesson(*blocks):
    arr = []
    for kind, val in blocks:
        key = "text" if kind == "text" else "code"
        arr.append(
            f"jsonb_build_object('kind', '{kind}', '{key}', $txt${val}$txt$)"
        )
    return "jsonb_build_array(\n    " + ",\n    ".join(arr) + "\n  )"


def content_starter(starter, **extra):
    parts = [f"'starter', $java${starter}$java$"]
    for k, v in extra.items():
        if k == "stdin":
            parts.append(f"'stdin', $txt${v}$txt$")
    return "jsonb_build_object(\n    " + ",\n    ".join(parts) + "\n  )"


def sample_code(code: str) -> str:
    return f"to_jsonb($java${code}$java$::text)"


def sample_text(text: str) -> str:
    return f"to_jsonb($txt${text}$txt$::text)"


def grading(obj) -> str:
    return "$j$" + json.dumps(obj, ensure_ascii=False) + "$j$::jsonb"


def predict_content(snippet, expected, accept=None):
    parts = [
        f"'snippet', $java${snippet}$java$",
        f"'expectedOutput', $txt${expected}$txt$",
    ]
    if accept:
        acc = ", ".join(f"$txt${a}$txt$" for a in accept)
        parts.append(f"'accept', jsonb_build_array({acc})")
    return "jsonb_build_object(\n    " + ",\n    ".join(parts) + "\n  )"


def predict_grading(expected, accept=None):
    obj = {"predict": {"compare": "normalized", "expectedOutput": expected}}
    if accept:
        obj["predict"]["accept"] = accept
    return grading(obj)


PREDICT_LESSON = lesson(
    (
        "text",
        "Predict the exact output of the snippet. Type your answer in the output box. "
        "Use a new line for each System.out.println (press Enter / return between lines). "
        "If the loop never stops, answer: infinite loop.",
    ),
)

INF_ACCEPT = [
    "infinite",
    "never stops",
    "never ends",
    "forever",
    "loops forever",
    "does not stop",
    "doesn't stop",
]


def row(slug, kind, title, desc, hint, lesson_sql, content_sql, sample_sql, grading_sql):
    hint_sql = "NULL" if hint is None else f"$txt${hint}$txt$"
    return f"""(
  '{slug}', '{kind}', '{title}',
  $txt${desc}$txt$,
  {hint_sql},
  {lesson_sql},
  {content_sql},
  {sample_sql},
  {grading_sql}
)"""


def build_day1_day2():
    rows = []

    rows.append(
        row(
            "hello-itu",
            "code",
            "Hello ITU",
            "Now it is your turn: print a sentence to say hello to your new university. Print exactly: Hello ITU!",
            'System.out.println("Hello ITU!");',
            lesson(
                (
                    "text",
                    "Printing a message is the most basic thing every programming language can do. In Java it takes a class, a main method, and one print statement:",
                ),
                (
                    "code",
                    """public class Hello {
    public static void main(String[] args) {
        System.out.println("Hello World!");
    }
}""",
                ),
            ),
            content_starter(
                """public class Main {
    public static void main(String[] args) {
        // Say hello to ITU
    }
}
"""
            ),
            sample_code(
                """public class Main {
    public static void main(String[] args) {
        System.out.println("Hello ITU!");
    }
}
"""
            ),
            grading({"target": "stdout", "op": "containsLine", "value": "Hello ITU!"}),
        )
    )

    rows.append(
        row(
            "print-three-values",
            "code",
            "Print three values",
            """Print three things, each on its own line:
1. A greeting with your name, like "Hello, my name is Aiting!"
2. The year you were born — a whole number
3. How many years you have lived in Copenhagen — with a decimal point (1.0 for exactly one year, 3.5 for three and a half)""",
            'Three println statements. 1996 is an int, 3.5 is a double, "Hello!" is a String.',
            lesson(
                (
                    "text",
                    "println can print more than text — whole numbers and decimal numbers work too. Notice that numbers need no quotes:",
                ),
                (
                    "code",
                    """System.out.println("Hello World!");
System.out.println(42);
System.out.println(3.14);""",
                ),
            ),
            content_starter(
                """public class Main {
    public static void main(String[] args) {
        // 1) a greeting  2) your birth year  3) years in Copenhagen (with a decimal)
    }
}
"""
            ),
            sample_code(
                """public class Main {
    public static void main(String[] args) {
        System.out.println("Hello, my name is Aiting!");
        System.out.println(1996);
        System.out.println(1.95);
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+$"},
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "(?m)^-?\\d+\\.\\d+$",
                        },
                        {"target": "stdout", "op": "regex", "pattern": "[A-Za-z]{2,}"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "use-variables",
            "code",
            "Use variables",
            "Print the same three values as before, but this time store each one in a variable first, then print the variable. Pick the right type: String for the greeting, int for the year, double for the years in Copenhagen.",
            'String greeting = "Hello, my name is …!"; then System.out.println(greeting);',
            lesson(
                (
                    "text",
                    "The same values can be stored in variables first. A variable has a type, a name, and a value:",
                ),
                (
                    "code",
                    """int x = 42;
System.out.println(x);

String s = "hi";
System.out.println(s);

double d = 3.14;
System.out.println(d);

boolean b = true;
System.out.println(b);""",
                ),
                (
                    "text",
                    """The four basic types:
int — whole numbers: 1, 0, -420, 2147483647
String — text in quotes: "hi", "hello world", "14b"
double — decimal numbers: 1.5, 3.1415, -27.15, 1.0
boolean — true or false""",
                ),
            ),
            content_starter(
                """public class Main {
    public static void main(String[] args) {
        // Declare a String, an int and a double — then print the variables
    }
}
"""
            ),
            sample_code(
                """public class Main {
    public static void main(String[] args) {
        String greeting = "Hello, my name is Aiting!";
        int birthYear = 1996;
        double yearsInCopenhagen = 1.95;

        System.out.println(greeting);
        System.out.println(birthYear);
        System.out.println(yearsInCopenhagen);
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": "\\bString\\s+\\w+\\s*="},
                        {"target": "code", "op": "regex", "pattern": "\\bint\\s+\\w+\\s*="},
                        {"target": "code", "op": "regex", "pattern": "\\bdouble\\s+\\w+\\s*="},
                        {"target": "stdout", "op": "regex", "pattern": "(?m)^-?\\d+$"},
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "(?m)^-?\\d+\\.\\d+$",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "variable-assignment",
            "code",
            "Variable assignment",
            'Use one int variable called age (starting at 27) to print "ITU is 27 years old." Then update the SAME variable with age = age + 1 and use it again to print "Next year ITU will be 28 years old."',
            "Build each sentence with String +, or use print/println.",
            lesson(
                (
                    "text",
                    "A variable can be given a new value later — that is why it is called a variable. The same variable then prints a different value:",
                ),
                (
                    "code",
                    """int year = 2026;
System.out.print("The year is ");
System.out.println(year);   // The year is 2026

year = year + 1;
System.out.print("The year is now ");
System.out.println(year);   // The year is now 2027""",
                ),
                (
                    "text",
                    "Fun fact: ITU is the youngest university in Denmark, founded in 1999 — it turns 27 in 2026.",
                ),
            ),
            content_starter(
                """public class Main {
    public static void main(String[] args) {
        int age = 27;
        // Print both sentences — update age in between
    }
}
"""
            ),
            sample_code(
                """public class Main {
    public static void main(String[] args) {
        int age = 27;
        System.out.println("ITU is " + age + " years old.");

        age = age + 1;
        System.out.println("Next year ITU will be " + age + " years old.");
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "ITU is 27 years old.",
                        },
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "Next year ITU will be 28 years old.",
                        },
                        {
                            "target": "code",
                            "op": "regex",
                            "pattern": "\\bage\\s*=\\s*age\\s*\\+\\s*1\\b|\\bage\\s*\\+\\+|\\bage\\s*\\+=\\s*1\\b",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "operators",
            "code",
            "Operators",
            """Fun facts from ITU Key figures 2025: about 23.4% of students are international. The Master of Software Design intake is around 130 students in recent years.

Print how many of those ~130 Soft Design masters students are international (approximately): 130 * 23.4 / 100.0 → 30.42

Use * and / in your code.""",
            "Use a double for 23.4 and divide by 100.0 so you keep the decimals.",
            lesson(
                (
                    "text",
                    "Java can calculate with + (plus), - (minus), * (multiply) and / (divide):",
                ),
                (
                    "code",
                    """System.out.println(3 + 3);   // 6

int x = 2;
System.out.println(x * x);   // 4

int y = 6;
System.out.println(y / 3);   // 2""",
                ),
                (
                    "text",
                    "Dividing two ints throws the decimals away. Use a double (e.g. 100.0 or 23.4) when you want a fractional result.",
                ),
            ),
            content_starter(
                """public class Main {
    public static void main(String[] args) {
        int mastersStudents = 130;
        double internationalPercent = 23.4;
        // Print international count
    }
}
"""
            ),
            sample_code(
                """public class Main {
    public static void main(String[] args) {
        int mastersStudents = 130;
        double internationalPercent = 23.4;

        double international = mastersStudents * internationalPercent / 100.0;
        System.out.println(international);
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "stdout", "op": "contains", "value": "30.42"},
                        {"target": "code", "op": "contains", "value": "*"},
                        {"target": "code", "op": "contains", "value": "/"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "string-concatenation",
            "code",
            "String concatenation",
            'The starter prints "Hello my friend!". Ask the person sitting next to you for their name, then modify the code to greet them personally: Hello my friend, {Name}!',
            'Add a String name = "…"; and concatenate it after "friend, ".',
            lesson(
                (
                    "text",
                    "+ between Strings glues them together — this is called concatenation:",
                ),
                (
                    "code",
                    """String hi = "Hello ";
String world = "World!";
String greet = hi + world;

System.out.println(greet);   // Hello World!""",
                ),
                ("text", "It also works between Strings and numbers:"),
                (
                    "code",
                    """int year = 2026;
System.out.println("The year is " + year);""",
                ),
            ),
            content_starter(
                """public class Hello {
    public static void main(String[] args) {
        String first = "Hello";
        String second = "my";
        String third = "friend";
        System.out.println(first + " " + second + " " + third + "!");
    }
}
"""
            ),
            sample_code(
                """public class Hello {
    public static void main(String[] args) {
        String first = "Hello";
        String second = "my";
        String third = "friend";
        String name = "Aiting";
        System.out.println(first + " " + second + " " + third + ", " + name + "!");
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "Hello my friend, .+!",
                        },
                        {"target": "code", "op": "contains", "value": "+"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "kroner-to-euro",
            "code",
            "Kroner to euro",
            'Modify the code so it converts the opposite way: from kroner to euro. For a 20 dkk coffee, print: "20 dkk corresponds to 2.6845637583892616 euro." (All the decimals are fine — that is just how doubles print.)',
            "Divide instead of multiply: dkk / 7.45",
            lesson(
                (
                    "text",
                    "During the break you meet a friend at ITU's own café, Cafe Analog. A coffee costs 20 dkk. Your friend says that is cheap — but you want to see it in euro. This code converts the other way, from euro to kroner:",
                ),
                (
                    "code",
                    """public class Valuta {
    public static void main(String[] args) {
        int eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr.");
    }
}""",
                ),
            ),
            content_starter(
                """public class Valuta {
    public static void main(String[] args) {
        int eur = 100;
        double dkk = eur * 7.45;
        System.out.println(eur + " euro corresponds to " + dkk + " kr.");
    }
}
"""
            ),
            sample_code(
                """public class Valuta {
    public static void main(String[] args) {
        int dkk = 20;
        double eur = dkk / 7.45;
        System.out.println(dkk + " dkk corresponds to " + eur + " euro.");
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "stdout", "op": "contains", "value": "20"},
                        {"target": "stdout", "op": "contains", "value": "corresponds to"},
                        {"target": "stdout", "op": "contains", "value": "euro"},
                        {"target": "stdout", "op": "regex", "pattern": "2\\.68"},
                        {"target": "code", "op": "regex", "pattern": "/\\s*7\\.45"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "functions",
            "code",
            "Functions",
            'The no-parameter version always converts 100 kr. Rewrite it so dkk2eur takes one parameter — the amount in dkk — and prints "{dkk} kr corresponds to {eur} euro". Call it twice from main: once with 20 (your Analog coffee) and once with another price, e.g. 15.',
            "static void dkk2eur(double dkk) { … }",
            lesson(
                (
                    "text",
                    "Functions let us reuse code and give a snippet a clear responsibility. This does exactly the same as the previous exercise, wrapped in a function:",
                ),
                (
                    "code",
                    """public class Valuta {
    static void dkk2eur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkk2eur();
    }
}""",
                ),
                (
                    "text",
                    "But this function always converts 100 kr. With a parameter, the same function works for any value:",
                ),
                (
                    "code",
                    """static void dkk2eur(double dkk) {
    double eur = dkk / 7.45;
    System.out.println(dkk + " kr corresponds to " + eur + " euro");
}

public static void main(String[] args) {
    dkk2eur(100);
    dkk2eur(20);
}""",
                ),
            ),
            content_starter(
                """public class Valuta {

    // Change this to take one parameter: static void dkk2eur(double dkk)
    static void dkk2eur() {
        double dkk = 100;
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        // Call dkk2eur twice with different prices
        dkk2eur();
    }
}
"""
            ),
            sample_code(
                """public class Valuta {

    static void dkk2eur(double dkk) {
        double eur = dkk / 7.45;
        System.out.println(dkk + " kr corresponds to " + eur + " euro");
    }

    public static void main(String[] args) {
        dkk2eur(20);
        dkk2eur(15);
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {
                            "target": "code",
                            "op": "regex",
                            "pattern": "static\\s+void\\s+\\w+\\s*\\(\\s*(?:int|double)\\s+\\w+\\s*\\)",
                        },
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "corresponds to",
                            "flags": "i",
                        },
                        {"target": "stdout", "op": "contains", "value": "euro"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "your-semester-in-ects",
            "code",
            "Your semester in ECTS",
            """Write a function printCourse(String name, double ects, int semester) that prints "{name} ({ects} ECTS) is in semester {semester}."

You are starting Software Design. Call it from main for all three semester-1 courses:
- Introductory Programming (15 ECTS)
- Discrete Mathematics (7.5 ECTS)
- Software Engineering (7.5 ECTS)
all in semester 1.""",
            "Three parameters: static void printCourse(String name, double ects, int semester)",
            lesson(
                (
                    "text",
                    "At ITU every course is worth ECTS points, and a full semester adds up to 30 ECTS. A function with several parameters can print any course the same way. Software Design semester 1: Introductory Programming (15), Discrete Mathematics (7.5), Software Engineering (7.5).",
                ),
            ),
            content_starter(
                """public class StudyPlan {

    // Declare printCourse here

    public static void main(String[] args) {
        // Print all three Soft Design semester-1 courses
    }
}
"""
            ),
            sample_code(
                """public class StudyPlan {

    static void printCourse(String name, double ects, int semester) {
        System.out.println(name + " (" + ects + " ECTS) is in semester " + semester + ".");
    }

    public static void main(String[] args) {
        printCourse("Introductory Programming", 15, 1);
        printCourse("Discrete Mathematics", 7.5, 1);
        printCourse("Software Engineering", 7.5, 1);
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {
                            "target": "code",
                            "op": "regex",
                            "pattern": "static\\s+void\\s+\\w+\\s*\\(\\s*String\\s+\\w+\\s*,\\s*(?:int|double)\\s+\\w+\\s*,\\s*int\\s+\\w+\\s*\\)",
                        },
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": ".+ \\(.+ ECTS\\) is in semester \\d+\\.",
                        },
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "Introductory Programming",
                        },
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "Discrete Mathematics",
                        },
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "Software Engineering",
                        },
                    ]
                }
            ),
        )
    )

    # ---- Day 2 code tasks ----
    rows.append(
        row(
            "at-itu-welcome",
            "code",
            "Welcome to ITU",
            'You just walked into the ITU atrium. You have a boolean atItu. Print "Welcome to ITU!" only if atItu is true. If you set atItu to false, the program should print nothing.',
            'if (atItu) { System.out.println("Welcome to ITU!"); }',
            lesson(
                (
                    "text",
                    "An if runs a block of code only when a condition is true. If the condition is false, Java simply skips the block and continues.",
                ),
                (
                    "code",
                    """if (condition) {
    System.out.println("Yes the condition is correct");
}""",
                ),
            ),
            content_starter(
                """public class Main {
    public static void main(String[] args) {
        boolean atItu = true;
        // Print Welcome to ITU! only if atItu is true
    }
}
"""
            ),
            sample_code(
                """public class Main {
    public static void main(String[] args) {
        boolean atItu = true;
        if (atItu) {
            System.out.println("Welcome to ITU!");
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": "\\bif\\s*\\("},
                        {
                            "target": "stdout",
                            "op": "containsLine",
                            "value": "Welcome to ITU!",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "scrollbar-friday",
            "code",
            "Scrollbar Friday",
            """Fun fact: Scrollbar is the Friday bar at ITU — open every Friday after 15:00 during the semester.

Write a program that prints whether it is Scrollbar day. One clear true case: Friday.

Set the day yourself with a String (no live calendar — BootIT is on Thursday, so you can flip the value to test both branches):

- If weekday == "Friday" → Yes, it is Friday, Scrollbar will open today!
- Otherwise → No, Scrollbar is closed.

Try both "Friday" and "Thursday".""",
            'if (weekday == "Friday") { ... } else { ... }',
            lesson(
                (
                    "text",
                    "With if / else, exactly one of the two branches runs — to be, or not to be:",
                ),
                (
                    "code",
                    """// code that is always executed

if (condition) {
    System.out.println("To be");
} else {
    System.out.println("Not to be");
}

// code that is always executed""",
                ),
            ),
            content_starter(
                """public class ScrollBar {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        // Print Yes... or No...
    }
}
"""
            ),
            sample_code(
                """public class ScrollBar {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        if (weekday == "Friday") {
            System.out.println("Yes, it is Friday, Scrollbar will open today!");
        } else {
            System.out.println("No, Scrollbar is closed.");
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": '==\\s*"Friday"'},
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "Yes|No",
                            "flags": "i",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "is-friday-boolean",
            "code",
            "Is Friday? (boolean)",
            """Rewrite the Scrollbar Friday check from the previous assignment using a boolean variable.

1. Keep a settable String weekday (e.g. "Friday" or "Thursday").
2. Create boolean isFriday = (weekday == "Friday");
3. Use if (isFriday) { ... } else { ... } (do not write if (isFriday == true)).
4. Keep the same two print messages.""",
            'boolean isFriday = (weekday == "Friday"); then if (isFriday) ...',
            lesson(
                (
                    "text",
                    "A boolean variable can take only two values: true and false.",
                ),
                (
                    "code",
                    """String weekday = "Thursday"; // BootIT day
boolean isThursday = (weekday == "Thursday");

// Idiomatic Java: use the boolean directly
if (isThursday) {
    System.out.println("It is Thursday");
}""",
                ),
            ),
            content_starter(
                """public class ScrollBar {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        // boolean isFriday = ...
        // if (isFriday) ...
    }
}
"""
            ),
            sample_code(
                """public class ScrollBar {
    public static void main(String[] args) {
        String weekday = "Friday"; // try "Thursday" too
        boolean isFriday = (weekday == "Friday");

        if (isFriday) {
            System.out.println("Yes, it is Friday, Scrollbar will open today!");
        } else {
            System.out.println("No, Scrollbar is closed.");
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {
                            "target": "code",
                            "op": "regex",
                            "pattern": "\\bboolean\\s+\\w+\\s*=",
                        },
                        {
                            "target": "code",
                            "op": "regex",
                            "pattern": "if\\s*\\(\\s*\\w+\\s*\\)",
                        },
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "Yes|No",
                            "flags": "i",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "canteen-lunch",
            "code",
            "Canteen lunch hours",
            """The ITU canteen serves lunch Monday–Friday, 11:00–14:00. After 13:45 there is a late-lunch discount.

Assume it is already a weekday — check the clock with nested conditions.

Use a double for the time of day (e.g. 11.0 = 11:00, 13.75 = 13:45).

- time < 11.0 → Too early — lunch starts at 11:00.
- time >= 14.0 → Too late — lunch ended at 14:00.
- otherwise:
  - print Lunch is being served.
  - then if time >= 13.75 → Late lunch discount applies! else → Full price.""",
            "Nest if/else. Try time = 13.75, 10.5, 12.0, 14.5.",
            lesson(
                (
                    "text",
                    "You can put an if inside another if. The inner block only runs when the outer condition is also true.",
                ),
                (
                    "code",
                    """int number = 42;

if (number > 0) {
    if (number > 100) {
        System.out.println("positive and big");
    } else {
        System.out.println("positive but small");
    }
} else {
    System.out.println("not positive");
}""",
                ),
            ),
            content_starter(
                """public class Canteen {
    public static void main(String[] args) {
        double time = 13.75; // try 10.5, 12.0, 13.5, 14.5
        // Nested if/else for early / late / serving + discount
    }
}
"""
            ),
            sample_code(
                """public class Canteen {
    public static void main(String[] args) {
        double time = 13.75; // try 10.5, 12.0, 13.5, 14.5

        if (time < 11.0) {
            System.out.println("Too early — lunch starts at 11:00.");
        } else {
            if (time >= 14.0) {
                System.out.println("Too late — lunch ended at 14:00.");
            } else {
                System.out.println("Lunch is being served.");
                if (time >= 13.75) {
                    System.out.println("Late lunch discount applies!");
                } else {
                    System.out.println("Full price.");
                }
            }
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "contains", "value": "if"},
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "Lunch is being served.",
                        },
                        {
                            "target": "stdout",
                            "op": "contains",
                            "value": "Late lunch discount applies!",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "fitness-access",
            "code",
            "Fitness access",
            """The ITU fitness room (basement, beneath Scrollbar) needs a membership — but on the first Tuesday of every month there is free trial access.

You may enter if you have a membership OR it is free-trial Tuesday:

if (hasMembership || isFreeTrialTuesday) print "Accessed" else print "Not allowed".

Flip the two booleans and check both outcomes.""",
            "if (hasMembership || isFreeTrialTuesday) { ... }",
            lesson(
                (
                    "text",
                    """Boolean comparisons: == != < > <= >=
Logical operators: ! (not), && (and), || (or) — at least one must be true for ||.""",
                ),
            ),
            content_starter(
                """public class FitnessAccess {
    public static void main(String[] args) {
        boolean hasMembership = false;
        boolean isFreeTrialTuesday = true; // first Tuesday of the month

        // if either is true → Accessed, else → Not allowed
    }
}
"""
            ),
            sample_code(
                """public class FitnessAccess {
    public static void main(String[] args) {
        boolean hasMembership = false;
        boolean isFreeTrialTuesday = true;

        if (hasMembership || isFreeTrialTuesday) {
            System.out.println("Accessed");
        } else {
            System.out.println("Not allowed");
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "contains", "value": "||"},
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "Accessed|Not allowed",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "student-day-place",
            "code",
            "A common day as an ITU student",
            """Write a function place(String period) that returns where a student probably is:

- morning → lectures, exercises, or a group room
- break → the Cafe Analog coffee queue
- noon → the canteen rush
- afternoon → the outdoor grass (if the sun is out)

From main, set a period and print:
System.out.println("During " + period + ", an ITU student is probably at " + place(period));""",
            "static String place(String period) { return ...; }",
            lesson(
                (
                    "text",
                    "Previous functions were mostly void (they printed). A function can also return a value to the caller with return. The return type goes where void used to be (String, int, boolean, …).",
                ),
            ),
            content_starter(
                """public class StudentDay {
    public static void main(String[] args) {
        String period = "morning"; // try "break", "noon", "afternoon"
        System.out.println("During " + period + ", an ITU student is probably at " + place(period));
    }

    static String place(String period) {
        // return the matching place
        return "";
    }
}
"""
            ),
            sample_code(
                """public class StudentDay {
    public static void main(String[] args) {
        String period = "morning";
        System.out.println("During " + period + ", an ITU student is probably at " + place(period));
    }

    static String place(String period) {
        if (period == "morning") {
            return "lectures, exercises, or a group room";
        } else if (period == "break") {
            return "the Cafe Analog coffee queue";
        } else if (period == "noon") {
            return "the canteen rush";
        } else {
            return "the outdoor grass (if the sun is out)";
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {
                            "target": "code",
                            "op": "regex",
                            "pattern": "static\\s+String\\s+\\w+\\s*\\(\\s*String\\s+\\w+\\s*\\)",
                        },
                        {"target": "code", "op": "contains", "value": "return"},
                        {
                            "target": "stdout",
                            "op": "regex",
                            "pattern": "During .+, an ITU student is probably at .+",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "while-five-lines",
            "code",
            "While loop — five lines",
            """Loops turn repetition into initialize → condition → body → increment.

Start from the starter. Right now it runs while i < 10 (i goes 0 … 9). Change only the loop condition so the program prints exactly 5 lines.

Hint: which number should replace 10, and why?""",
            "while (i < 5) runs for i = 0,1,2,3,4 — exactly 5 times.",
            lesson(
                ("text", "Without a loop you copy-paste. With while:"),
                (
                    "code",
                    """int i = 0;                 // initialization
while (i < 10) {           // loop condition
    System.out.println("I will push my code before the deadline!");
    i = i + 1;             // increment
}""",
                ),
                (
                    "text",
                    "i starts at 0, condition i < 10 → body runs for 0..9 (10 times). It never runs with i == 10.",
                ),
            ),
            content_starter(
                """public class Number {

    static void function(int number) {
        System.out.println("Line " + number + ": I will push my code before the deadline!");
    }

    public static void main(String[] args) {
        int i = 0; // initialization
        while (i < 10) { // loop condition — change this
            function(i);
            i = i + 1; // increment
        }
    }
}
"""
            ),
            sample_code(
                """public class Number {

    static void function(int number) {
        System.out.println("Line " + number + ": I will push my code before the deadline!");
    }

    public static void main(String[] args) {
        int i = 0;
        while (i < 5) { // 0,1,2,3,4 → exactly 5 lines
            function(i);
            i = i + 1;
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "contains", "value": "while"},
                        {"target": "stdout", "op": "contains", "value": "Line 0:"},
                        {"target": "stdout", "op": "contains", "value": "Line 4:"},
                        {
                            "not": {
                                "target": "stdout",
                                "op": "contains",
                                "value": "Line 5:",
                            }
                        },
                    ]
                }
            ),
        )
    )

    quizzes_while = [
        (
            "while-loop-quiz-1",
            "While Loop Quiz 1",
            """int i = 10;
while (i > 0) {
    System.out.println(i);
    i = i - 1;
}""",
            "10\n9\n8\n7\n6\n5\n4\n3\n2\n1",
            None,
        ),
        (
            "while-loop-quiz-2",
            "While Loop Quiz 2",
            """int i = 1;
while (i <= 10) {
    System.out.println(i);
    i = i + 2;
}""",
            "1\n3\n5\n7\n9",
            None,
        ),
        (
            "while-loop-quiz-3",
            "While Loop Quiz 3",
            """int i = 1;
while (i < 100) {
    System.out.println(i);
    i = i * 2;
}""",
            "1\n2\n4\n8\n16\n32\n64",
            None,
        ),
        (
            "while-loop-quiz-4",
            "While Loop Quiz 4",
            """int i = 1;
while (i < 42) {
    System.out.println(i);
    i = i * i;
}""",
            "infinite loop",
            INF_ACCEPT,
        ),
        (
            "while-loop-quiz-5",
            "While Loop Quiz 5",
            """int i = 0;
while (i <= 15) {
    System.out.println(i);
    i = i + 3;
}""",
            "0\n3\n6\n9\n12\n15",
            None,
        ),
        (
            "while-loop-quiz-6",
            "While Loop Quiz 6",
            """int i = 64;
while (i >= 2) {
    System.out.println(i);
    i = i / 2;
}""",
            "64\n32\n16\n8\n4\n2",
            None,
        ),
    ]

    for slug, title, snip, exp, acc in quizzes_while:
        hint = "What is 1 × 1? Does i ever change?" if acc else None
        desc = (
            "Careful with this one! Predict what it prints — some loops never stop."
            if acc
            else "Read the loop and predict exactly what it prints. Type your answer in the output window; use return/enter for each println line."
        )
        rows.append(
            row(
                slug,
                "predict",
                title,
                desc,
                hint,
                PREDICT_LESSON,
                predict_content(snip, exp, acc),
                sample_text(exp),
                predict_grading(exp, acc),
            )
        )

    rows.append(
        row(
            "analog-tickets-while",
            "code",
            "Analog tickets (while)",
            """Cafe Analog has a mobile app where you can buy 5 tickets at once with a discount. You are a heavy coffee drinker and want 50 tickets in one go — so you need a loop that buys a pack of 5 tickets, ten times.

Using a while loop, print the running total after each pack: 5, 10, 15, …, 50 (one number per line).""",
            "Accumulate total += ticketsPerPack inside while (packs < 10).",
            lesson(
                (
                    "text",
                    "Use a while loop when you know you need to repeat until a counter reaches a limit. Initialize → check → body → increment.",
                ),
            ),
            content_starter(
                """public class AnalogTickets {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        // Buy 10 packs with a while loop; print the running total each time
    }
}
"""
            ),
            sample_code(
                """public class AnalogTickets {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        int total = 0;
        int packs = 0;
        while (packs < 10) {
            total = total + ticketsPerPack;
            System.out.println(total);
            packs = packs + 1;
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "contains", "value": "while"},
                        {"target": "stdout", "op": "containsLine", "value": "50"},
                        {"target": "stdout", "op": "containsLine", "value": "45"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "analog-tickets-for",
            "code",
            "Analog tickets (for)",
            "Rewrite the Cafe Analog tickets program. Same output (5 … 50), but use a for loop instead of while. The starter is the while solution — change it to for.",
            "for (int packs = 0; packs < 10; packs = packs + 1) { ... }",
            lesson(
                (
                    "text",
                    "The for loop packs initialization, condition, and increment into one header — nicer when you already know how many times to repeat:",
                ),
                (
                    "code",
                    """for (int i = 0; i < 10; i = i + 1) {
    System.out.println(i);
}""",
                ),
                ("text", "A while loop and a for loop can do the same job."),
            ),
            content_starter(
                """public class AnalogTickets {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        int total = 0;
        int packs = 0;
        while (packs < 10) {
            total = total + ticketsPerPack;
            System.out.println(total);
            packs = packs + 1;
        }
    }
}
"""
            ),
            sample_code(
                """public class AnalogTickets {
    public static void main(String[] args) {
        int ticketsPerPack = 5;
        int total = 0;
        for (int packs = 0; packs < 10; packs = packs + 1) {
            total = total + ticketsPerPack;
            System.out.println(total);
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\("},
                        {
                            "not": {
                                "target": "code",
                                "op": "regex",
                                "pattern": "\\bwhile\\s*\\(",
                            }
                        },
                        {"target": "stdout", "op": "containsLine", "value": "50"},
                        {"target": "stdout", "op": "containsLine", "value": "45"},
                    ]
                }
            ),
        )
    )

    quizzes_for = [
        (
            "for-loop-quiz-1",
            "For Loop Quiz 1",
            """for (int i = 10; i > 0; i = i - 2) {
    System.out.println(i);
}""",
            "10\n8\n6\n4\n2",
            None,
        ),
        (
            "for-loop-quiz-2",
            "For Loop Quiz 2",
            """for (int i = 1; i < 10; i = i + 3) {
    System.out.println(i);
}""",
            "1\n4\n7",
            None,
        ),
        (
            "for-loop-quiz-3",
            "For Loop Quiz 3",
            """for (int i = 1; i < 10; i = i * i) {
    System.out.println(i);
}""",
            "infinite loop",
            INF_ACCEPT,
        ),
        (
            "for-loop-quiz-4",
            "For Loop Quiz 4",
            """for (int i = 0; i <= 15; i = i + 3) {
    System.out.println(i);
}""",
            "0\n3\n6\n9\n12\n15",
            None,
        ),
        (
            "for-loop-quiz-5",
            "For Loop Quiz 5",
            """for (int i = 1; i <= 10000; i = i * 10) {
    System.out.println(i);
}""",
            "1\n10\n100\n1000\n10000",
            None,
        ),
        (
            "for-loop-quiz-6",
            "For Loop Quiz 6",
            """for (int i = 64; i >= 2; i = i / 2) {
    System.out.println(i);
}""",
            "64\n32\n16\n8\n4\n2",
            None,
        ),
    ]

    for slug, title, snip, exp, acc in quizzes_for:
        hint = "What is 1 × 1? Does i ever grow?" if acc else None
        desc = (
            "Careful with this one! Predict what it prints — some loops never stop."
            if acc
            else "Read the loop and predict exactly what it prints."
        )
        rows.append(
            row(
                slug,
                "predict",
                title,
                desc,
                hint,
                PREDICT_LESSON,
                predict_content(snip, exp, acc),
                sample_text(exp),
                predict_grading(exp, acc),
            )
        )

    rows.append(
        row(
            "gym-workout",
            "code",
            "Gym workout (nested for)",
            """You are in the ITU fitness room. Today's plan: 4 sets, 12 reps each. Use a nested for to log every rep — outer loop = set (1…4), inner loop = rep (1…12):

Set 1 Rep 1
…
Set 4 Rep 12""",
            "for (int set = 1; set <= 4; set++) { for (int rep = 1; rep <= 12; rep++) { ... } }",
            lesson(
                (
                    "text",
                    "Sometimes you need two counters. A nested for reads like the shape of the data:",
                ),
                (
                    "code",
                    """for (int set = 1; set <= 2; set = set + 1) {
    for (int rep = 1; rep <= 3; rep = rep + 1) {
        System.out.println("Set " + set + " Rep " + rep);
    }
}""",
                ),
            ),
            content_starter(
                """public class GymWorkout {
    public static void main(String[] args) {
        // nested for: sets 1..4, reps 1..12
    }
}
"""
            ),
            sample_code(
                """public class GymWorkout {
    public static void main(String[] args) {
        for (int set = 1; set <= 4; set = set + 1) {
            for (int rep = 1; rep <= 12; rep = rep + 1) {
                System.out.println("Set " + set + " Rep " + rep);
            }
        }
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": "\\bfor\\s*\\("},
                        {
                            "target": "stdout",
                            "op": "containsLine",
                            "value": "Set 1 Rep 1",
                        },
                        {
                            "target": "stdout",
                            "op": "containsLine",
                            "value": "Set 4 Rep 12",
                        },
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "guess-locker",
            "code",
            "Guess the locker",
            'You can rent a locker at the Information Desk (deposit 500 DKK). Today you forgot which locker is yours — a secret integer from 0 … 99. Read guesses with a Scanner in a while loop, print "Too low" / "Too high" until correct, then congratulate and print how many guesses you used.',
            "Scanner scanner = new Scanner(System.in); while (guess != secret) { guess = scanner.nextInt(); ... }",
            lesson(
                (
                    "text",
                    "Expressions produce values; statements do something with them (assignments, if, while/for, calls). A guessing game glues these together.",
                ),
            ),
            content_starter(
                """import java.util.*;

public class Guess {
    public static void main(String[] args) {
        Random random = new Random();
        Scanner scanner = new Scanner(System.in);
        int secret = random.nextInt(100);
        int tries = 0;
        int guess = -1;
        // Loop until the guess equals secret; print Too low / Too high
    }
}
""",
                stdin="50\n25\n37\n31\n34\n",
            ),
            sample_code(
                """import java.util.*;

public class Guess {
    public static void main(String[] args) {
        Random random = new Random();
        Scanner scanner = new Scanner(System.in);
        int secret = random.nextInt(100);
        int tries = 0;
        int guess = -1;

        while (guess != secret) {
            System.out.println("Make a guess:");
            guess = scanner.nextInt();
            tries = tries + 1;
            if (guess < secret) {
                System.out.println("Too low");
            } else if (guess > secret) {
                System.out.println("Too high");
            }
        }

        System.out.println("Correct — that is your locker!");
        System.out.println("You used " + tries + " guesses");
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": "Scanner"},
                        {"target": "code", "op": "regex", "pattern": "while"},
                        {"target": "code", "op": "regex", "pattern": "nextInt"},
                    ]
                }
            ),
        )
    )

    rows.append(
        row(
            "how-many-ab",
            "code",
            "Bulls and cows (幾A幾B)",
            """Bonus: guess a secret 4-digit code (all digits different), e.g. "1704".

After each guess print how many A and B:
- A = correct digit in the correct position
- B = correct digit in the wrong position

Example: secret 1704, guess 1075 → 1A2B.

Keep looping until 4A0B, then congratulate. Start with a hardcoded secret so you can test.""",
            "Use guess.charAt(i) == secret.charAt(i) for A; nested loops for B.",
            lesson(
                (
                    "text",
                    "This bonus mixes Scanner, while, for, if, and Strings. Compare digits with charAt. Print results like 1A2B until you reach 4A0B.",
                ),
            ),
            content_starter(
                """import java.util.*;

public class HowManyAB {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        String secret = "1704"; // all digits different
        String guess = "";

        // while not 4A:
        //   read guess
        //   count A and B
        //   print e.g. "1A2B"
        // print a win message
    }
}
""",
                stdin="1075\n1704\n",
            ),
            sample_code(
                """import java.util.*;

public class HowManyAB {
    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);
        String secret = "1704";
        int a = 0;

        while (a != 4) {
            System.out.println("Guess a 4-digit code:");
            String guess = scanner.next();
            a = 0;
            int b = 0;

            for (int i = 0; i < 4; i = i + 1) {
                if (guess.charAt(i) == secret.charAt(i)) {
                    a = a + 1;
                }
            }

            for (int i = 0; i < 4; i = i + 1) {
                if (guess.charAt(i) == secret.charAt(i)) {
                    continue;
                }
                for (int j = 0; j < 4; j = j + 1) {
                    if (guess.charAt(i) == secret.charAt(j) && i != j) {
                        b = b + 1;
                        break;
                    }
                }
            }

            System.out.println(a + "A" + b + "B");
        }

        System.out.println("You cracked the code!");
    }
}
"""
            ),
            grading(
                {
                    "all": [
                        {"target": "code", "op": "regex", "pattern": "Scanner"},
                        {"target": "code", "op": "regex", "pattern": "while"},
                        {"target": "code", "op": "regex", "pattern": "charAt"},
                    ]
                }
            ),
        )
    )

    assert len(rows) == 33, len(rows)
    return rows


def extract_day3(sql: str) -> str:
    m = re.search(
        r"\(\s*\n\s*'person-class'.*?'grandmas-blackmarket-kitchen'.*?NULL\s*\n\)",
        sql,
        re.S,
    )
    if not m:
        raise SystemExit("day3 block not found in existing seed-tasks.sql")
    return m.group(0)


def main():
    rows = build_day1_day2()
    existing = OUT.read_text() if OUT.exists() else SRC.read_text()
    day3 = extract_day3(existing)

    slugs = [
        "hello-itu",
        "print-three-values",
        "use-variables",
        "variable-assignment",
        "operators",
        "string-concatenation",
        "kroner-to-euro",
        "functions",
        "your-semester-in-ects",
        "at-itu-welcome",
        "scrollbar-friday",
        "is-friday-boolean",
        "canteen-lunch",
        "fitness-access",
        "student-day-place",
        "while-five-lines",
        "while-loop-quiz-1",
        "while-loop-quiz-2",
        "while-loop-quiz-3",
        "while-loop-quiz-4",
        "while-loop-quiz-5",
        "while-loop-quiz-6",
        "analog-tickets-while",
        "analog-tickets-for",
        "for-loop-quiz-1",
        "for-loop-quiz-2",
        "for-loop-quiz-3",
        "for-loop-quiz-4",
        "for-loop-quiz-5",
        "for-loop-quiz-6",
        "gym-workout",
        "guess-locker",
        "how-many-ab",
        "person-class",
        "flight-ticket-class",
        "container-class",
        "build-a-tree",
        "grandpas-time-machine",
        "grandmas-blackmarket-kitchen",
    ]
    assert len(slugs) == 39

    ordered = ",\n".join(f"  ('{s}', {i})" for i, s in enumerate(slugs))

    header = """-- ============================================================================
-- seed-tasks.sql — BootIT assignments + assignment sets (Day 1–3).
-- Content sourced from BootIT DAY 1.md / DAY 2.md (2026 rewrite) + existing Day 3.
--
-- Usage (schema must already exist via `dotnet ef database update`):
--
--     psql "$CONNECTION_STRING" -f scripts/seed-tasks.sql
--     (use a postgresql:// URI or PG* env vars — not the Npgsql Host=...; form)
--
-- Full reset each run:
--   * TRUNCATE assignment-related tables with RESTART IDENTITY so ids start at 1.
--   * Then INSERT all rows fresh (including sample_solution_json).
--   * Does NOT wipe students / sessions / attendance.
--
-- Conventions (see SCHEMA.md):
--   * assignment.id is DB-assigned — never written here. References use slug.
--   * kind is lowercase text: 'code' | 'predict' | 'project'.
--   * Day 1–2: lesson_json, content_json, sample_solution_json, grading_json
--     are always populated (hint may be NULL).
--   * Predict grading_json documents expectedOutput/accept (SubmissionService
--     currently only auto-grades kind=code).
--
-- Counts: Day 1 = 9, Day 2 = 24, Day 3 = 6, total = 39.
-- ============================================================================

BEGIN;

-- Wipe seed tables and restart identity sequences (assignment.id back to 1).
TRUNCATE TABLE
  submission,
  assignment_set_assignment,
  assignment,
  assignment_set
RESTART IDENTITY CASCADE;

-- ────────────────────────────── assignment sets ────────────────────────────

INSERT INTO assignment_set (assignment_set_id, display_title) VALUES
  ('day1-2026',                     'BootIT Day 1 — 2026'),
  ('day2-2026',                     'BootIT Day 2 — 2026'),
  ('day3-2026',                     'BootIT Day 3 — 2026'),
  ('all-assignments-for-solo-2026', 'BootIT — All Tasks (Solo) 2026');

-- ──────────────────────────────── assignments ──────────────────────────────

INSERT INTO assignment (slug, kind, title, description, hint, lesson_json, content_json, sample_solution_json, grading_json) VALUES

-- ─────────────────────────── DAY 1 — basics ───────────────────────────
"""

    footer = f"""
;

-- ─────────────────────────── set memberships ───────────────────────────
--   Day 1: 0–8 (9)   Day 2: 9–32 (24)   Day 3: 33–38 (6)   total 39

WITH ordered(slug, ord) AS (VALUES
{ordered}
),
resolved AS (
  SELECT t.id AS assignment_id, o.ord
  FROM ordered o
  JOIN assignment t ON t.slug = o.slug
)
INSERT INTO assignment_set_assignment (assignment_set_id, assignment_id, order_index)
SELECT 'day1-2026', assignment_id, ord         FROM resolved WHERE ord BETWEEN 0 AND 8
UNION ALL
SELECT 'day2-2026', assignment_id, ord - 9     FROM resolved WHERE ord BETWEEN 9 AND 32
UNION ALL
SELECT 'day3-2026', assignment_id, ord - 33    FROM resolved WHERE ord BETWEEN 33 AND 38
UNION ALL
SELECT 'all-assignments-for-solo-2026', assignment_id, ord FROM resolved;

DO $check$
DECLARE n int;
BEGIN
  SELECT count(*) INTO n FROM assignment_set_assignment WHERE assignment_set_id = 'all-assignments-for-solo-2026';
  IF n <> 39 THEN
    RAISE EXCEPTION 'seed error: expected 39 assignments in all-assignments-for-solo-2026, got % (typo in a slug?)', n;
  END IF;
END
$check$;

COMMIT;
"""

    body = (
        ",\n".join(rows[:9])
        + ",\n\n-- ─────────────────────────── DAY 2 — conditionals, loops, input ───────────────────────────\n"
        + ",\n".join(rows[9:])
        + ",\n\n-- ─────────────────────────── DAY 3 — classes & objects / projects ───────────────────────────\n"
        + day3
    )

    OUT.write_text(header + body + footer)
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes), day1-2 rows={len(rows)}")


if __name__ == "__main__":
    main()
