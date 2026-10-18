import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { fullHome } from '../test/fixtures'
import { Hero } from './Hero'
import { Story } from './Story'

const home = {
  ...fullHome,
  settings: {
    ...fullHome.settings,
    hero_image_url: 'https://example.test/hero.webp',
    story_image_url: 'https://example.test/story.webp',
  },
}

describe('Hero image', () => {
  it('is fetched first and never lazy; the rest of the page waits', () => {
    render(
      <>
        <Hero home={home} />
        <Story home={home} />
      </>,
    )

    const [hero, story] = screen.getAllByRole('img')
    expect(hero).toHaveAttribute('src', 'https://example.test/hero.webp')
    expect(hero).toHaveAttribute('loading', 'eager')
    expect(hero).toHaveAttribute('fetchpriority', 'high')

    expect(story).toHaveAttribute('loading', 'lazy')
    expect(story).not.toHaveAttribute('fetchpriority', 'high')
  })
})
