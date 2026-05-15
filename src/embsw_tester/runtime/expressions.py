from __future__ import annotations

import ast
import re
from typing import Any, Mapping


class ExpressionError(RuntimeError):
    """Raised when a DSL expression cannot be evaluated safely."""


_EXPRESSION_PATTERN = re.compile(r"^\$\{(?P<body>.*)\}$", re.DOTALL)
_TEMPLATE_PATTERN = re.compile(r"\{\{\s*(?P<body>.*?)\s*\}\}")
_LITERAL_REPLACEMENTS = (
    (re.compile(r"\btrue\b"), "True"),
    (re.compile(r"\bfalse\b"), "False"),
    (re.compile(r"\bnull\b"), "None"),
)

_ALLOWED_NODES = (
    ast.Expression,
    ast.BoolOp,
    ast.BinOp,
    ast.UnaryOp,
    ast.Compare,
    ast.Name,
    ast.Load,
    ast.Constant,
    ast.And,
    ast.Or,
    ast.Not,
    ast.USub,
    ast.UAdd,
    ast.Add,
    ast.Sub,
    ast.Mult,
    ast.Div,
    ast.Mod,
    ast.Eq,
    ast.NotEq,
    ast.Lt,
    ast.LtE,
    ast.Gt,
    ast.GtE,
)


def evaluate_value(value: Any, variables: Mapping[str, Any]) -> Any:
    if isinstance(value, str):
        expression_match = _EXPRESSION_PATTERN.match(value.strip())
        if expression_match:
            return evaluate_expression(expression_match.group("body"), variables)
        return render_template(value, variables)
    if isinstance(value, list):
        return [evaluate_value(item, variables) for item in value]
    if isinstance(value, dict):
        return {key: evaluate_value(item, variables) for key, item in value.items()}
    return value


def render_template(value: str, variables: Mapping[str, Any]) -> str:
    def replace(match: re.Match[str]) -> str:
        return str(evaluate_expression(match.group("body"), variables))

    return _TEMPLATE_PATTERN.sub(replace, value)


def evaluate_expression(expression: str, variables: Mapping[str, Any]) -> Any:
    normalized = _normalize_literals(expression.strip())
    try:
        tree = ast.parse(normalized, mode="eval")
    except SyntaxError as exc:
        raise ExpressionError(f"Invalid expression '{expression}'.") from exc

    for node in ast.walk(tree):
        if not isinstance(node, _ALLOWED_NODES):
            raise ExpressionError(f"Unsupported expression element '{type(node).__name__}'.")

    try:
        return eval(compile(tree, "<dsl-expression>", "eval"), {"__builtins__": {}}, dict(variables))
    except NameError as exc:
        raise ExpressionError(str(exc)) from exc


def _normalize_literals(expression: str) -> str:
    normalized = expression
    for pattern, replacement in _LITERAL_REPLACEMENTS:
        normalized = pattern.sub(replacement, normalized)
    return normalized
