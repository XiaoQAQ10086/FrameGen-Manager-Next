#[derive(Clone, Debug)]
pub(crate) enum ValveValue {
    String(String),
    Object(Vec<(String, ValveValue)>),
}

#[derive(Clone, Debug)]
pub(crate) struct ValveDocument {
    entries: Vec<(String, ValveValue)>,
}

impl ValveDocument {
    pub(crate) fn parse(text: &str) -> Option<Self> {
        let tokens = tokenize(text)?;
        let mut position = 0;
        let entries = parse_entries(&tokens, &mut position)?;
        (position == tokens.len()).then_some(Self { entries })
    }

    pub(crate) fn all_strings(&self, key: &str) -> Vec<String> {
        let mut values = Vec::new();
        collect_strings(&self.entries, key, &mut values);
        values
    }

    pub(crate) fn object(&self, key: &str) -> Option<&[(String, ValveValue)]> {
        self.entries
            .iter()
            .find_map(|(name, value)| (name.eq_ignore_ascii_case(key)).then_some(value))
            .and_then(|value| match value {
                ValveValue::Object(entries) => Some(entries.as_slice()),
                ValveValue::String(_) => None,
            })
    }
}

fn collect_strings(entries: &[(String, ValveValue)], key: &str, values: &mut Vec<String>) {
    for (name, value) in entries {
        match value {
            ValveValue::String(text) if name.eq_ignore_ascii_case(key) => {
                values.push(text.clone());
            }
            ValveValue::Object(children) => collect_strings(children, key, values),
            ValveValue::String(_) => {}
        }
    }
}

fn parse_entries(tokens: &[Token], position: &mut usize) -> Option<Vec<(String, ValveValue)>> {
    let mut entries = Vec::new();
    loop {
        let Some(token) = tokens.get(*position) else {
            return Some(entries);
        };
        match token {
            Token::Close => return Some(entries),
            Token::Open => return None,
            Token::Text(key) => {
                *position += 1;
                let value = match tokens.get(*position)? {
                    Token::Text(value) => {
                        *position += 1;
                        ValveValue::String(value.clone())
                    }
                    Token::Open => {
                        *position += 1;
                        let children = parse_entries(tokens, position)?;
                        if !matches!(tokens.get(*position), Some(Token::Close)) {
                            return None;
                        }
                        *position += 1;
                        ValveValue::Object(children)
                    }
                    Token::Close => return None,
                };
                entries.push((key.clone(), value));
            }
        }
    }
}

enum Token {
    Text(String),
    Open,
    Close,
}

fn tokenize(text: &str) -> Option<Vec<Token>> {
    let bytes = text.as_bytes();
    let mut tokens = Vec::new();
    let mut index = 0;

    while index < bytes.len() {
        let byte = bytes[index];
        if byte.is_ascii_whitespace() {
            index += 1;
            continue;
        }
        if byte == b'/' && bytes.get(index + 1) == Some(&b'/') {
            while index < bytes.len() && bytes[index] != b'\n' {
                index += 1;
            }
            continue;
        }
        if byte == b'{' {
            tokens.push(Token::Open);
            index += 1;
            continue;
        }
        if byte == b'}' {
            tokens.push(Token::Close);
            index += 1;
            continue;
        }
        if byte == b'"' {
            index += 1;
            let mut value = Vec::new();
            while index < bytes.len() {
                match bytes[index] {
                    b'"' => {
                        index += 1;
                        break;
                    }
                    b'\\' => {
                        index += 1;
                        let escaped = *bytes.get(index)?;
                        value.push(match escaped {
                            b'n' => b'\n',
                            b't' => b'\t',
                            other => other,
                        });
                        index += 1;
                    }
                    other => {
                        value.push(other);
                        index += 1;
                    }
                }
            }
            tokens.push(Token::Text(String::from_utf8_lossy(&value).into_owned()));
            continue;
        }

        let start = index;
        while index < bytes.len()
            && !bytes[index].is_ascii_whitespace()
            && !matches!(bytes[index], b'{' | b'}')
        {
            index += 1;
        }
        if start == index {
            return None;
        }
        tokens.push(Token::Text(
            String::from_utf8_lossy(&bytes[start..index]).into_owned(),
        ));
    }

    Some(tokens)
}
