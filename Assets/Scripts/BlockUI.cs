using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler ,IDropHandler
{
    private string key;
   
    public GameObject move_prefab;

    GameObject move_image;

    private bool complete = false;


    public void SetKey(string n) { key = n; }

    private Vector2 SetSizeNorm(Vector2 size, float n)
    {
        float stand = size.x;
        if (size.y > size.x) stand = size.y;
        size /= stand;
        return size * n;
    }

    IEnumerator FadeInOut()
    {
        float f = 0.1f;
        Color color = move_image.GetComponent<Image>().color;
        while (true)
        {
            if (color.a <= 0.2f || color.a >= 1.0f)
                f *= -1.0f;
            color.a += f;
            move_image.GetComponent<Image>().color = color;
            yield return new WaitForSeconds(0.07f);
        }
    }


    private void Start()
    {
        transform.localScale = SetSizeNorm(GameManager.Instance.Puzzle[key].ui.bounds.size, 1);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        //드래그 중인 퍼즐 key
        GameManager.Instance.drag_block_id = key;

        //moveImage 새로 생성
        GameManager.Instance.move_canvas.SetActive(true);
        move_prefab.GetComponent<Image>().sprite = GameManager.Instance.Puzzle[key].ui;
        move_image = Instantiate<GameObject>(move_prefab, GameManager.Instance.move_canvas.transform);
        move_image.transform.position = transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        move_image.transform.position = eventData.position;
        move_image.transform.localScale = transform.localScale;
        float d = Vector2.Distance(GameManager.Instance.Puzzle[key].pos, move_image.transform.position);
        if(GameManager.Instance.Puzzle[key].dir == GameManager.Instance.camera_dir)
            if (d <= GameManager.Instance.block_distance && !complete)
            {
                StartCoroutine("FadeInOut");
                complete = true;
            }
            else if(d >= GameManager.Instance.block_distance && complete)
            {
                StopCoroutine("FadeInOut");
                Color color = move_image.GetComponent<Image>().color;
                color.a = 1.0f;
                move_image.GetComponent<Image>().color = color;
                complete = false;
            }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (complete)
        {
            GameManager.Instance.Puzzle[key].block.SetActive(true);
            Destroy(move_image.gameObject);
            move_image = null;
            Destroy(gameObject);
        }
        else
        {
            Destroy(move_image.gameObject);
            move_image = null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {

    }

}
